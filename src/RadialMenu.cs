using System;
using SecretHistories.Enums;
using SecretHistories.Infrastructure;
using SecretHistories.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>What the wheel needs from the mod to show and act on slots.</summary>
	public interface ILocationActions
	{
		/// <summary>The key this slot is bound to right now, for the wheel's key caps.</summary>
		string SlotKeyLabel(int slot);

		bool TryGetLocation(int slot, out SavedLocation location);

		Texture2D PreviewFor(int slot);

		void JumpToSlot(int slot);

		void BindSlot(int slot);

		void ClearSlot(int slot);

		void RefreshPreview(int slot);

		void RenameSlot(int slot, string name);
	}

	/// <summary>
	/// The location wheel: every slot at once, with a thumbnail and a name, on the
	/// game's own overlay canvas.
	///
	/// Hover is resolved by direction from the wheel's centre rather than by pointer
	/// events on each slot, which is what lets you flick towards a slot and release
	/// without having to land on it. It also means holding and clicking share one
	/// code path.
	/// </summary>
	public class RadialMenu
	{
		private const int SlotCount = GameBindings.SlotCount;

		/// <summary>Inside this radius of the centre nothing is selected, so there's
		/// always somewhere to release the key that means "never mind". Doubles as the
		/// hub's radius, since the hub is what the dead zone looks like.</summary>
		private const float DeadZoneRadius = 122f;

		/// <summary>
		/// The slots are laid out on an ellipse rather than a circle. They're half
		/// again as wide as they are tall, so on a circle the ones nearest twelve and
		/// six o'clock - where the arc runs horizontally - crowd together and clip,
		/// while the ones at three and nine have room to spare. Stretching the
		/// horizontal radius evens the gaps out.
		/// </summary>
		private const float HorizontalStretch = 1.4f;

		private const float WedgeWidth = 150f;

		private const float WedgeHeight = 100f;

		private const int NameLengthLimit = 32;

		private readonly HotkeyConfig _config;

		private readonly ILocationActions _actions;

		private RectTransform _root;

		private RectTransform _ring;

		private UnscaledFader _fader;

		private Wedge[] _wedges;

		private TextMeshProUGUI _centreLabel;

		private TextMeshProUGUI _legend;

		private RectTransform _legendPanel;

		private bool _open;

		/// <summary>Opened with a tap, so it stays up instead of tracking the held key.</summary>
		private bool _pinned;

		/// <summary>
		/// Whether we yet know if this is a tap or a hold. Until the key is released
		/// or the tap threshold passes, it could be either, and the two want opposite
		/// prompts - so the prompt stays blank rather than guessing and correcting
		/// itself a moment later.
		/// </summary>
		private bool _modeDecided;

		private float _openedAt;

		/// <summary>Slot under the cursor, 1-based; 0 for none.</summary>
		private int _hovered;

		private bool _renaming;

		private int _renamingSlot;

		private TMP_InputField _renameField;

		/// <summary>Frame the rename finished on. The EventSystem may end an edit by
		/// click before our Update runs, and that same click must not also pick a
		/// location out from under it.</summary>
		private int _renameEndedFrame = -1;

		/// <summary>True while we're the ones holding the game's input disabled.</summary>
		private bool _inputDisabledByUs;

		public RadialMenu(HotkeyConfig config, ILocationActions actions)
		{
			_config = config;
			_actions = actions;
		}

		public bool IsOpen => _open;

		public bool IsRenaming => _renaming;

		/// <summary>One slot's plate on the wheel.</summary>
		private class Wedge
		{
			public RectTransform Root;

			public Image Plate;

			public Image Border;

			public RawImage Preview;

			public TextMeshProUGUI Placeholder;

			public TextMeshProUGUI KeyCap;
		}

		public void Open()
		{
			if (_open || !Build())
			{
				return;
			}

			_open = true;
			_pinned = false;
			_modeDecided = false;
			_openedAt = Time.unscaledTime;
			_hovered = 0;

			Refresh();
			_root.gameObject.SetActive(true);
			_root.SetAsLastSibling();
			_fader.Show(true);

			CentreCursor();
			DisableGameInput();
			NativeUi.Sfx(AudioEvent.InfoWindowShow);
		}

		public void Close()
		{
			if (!_open)
			{
				return;
			}

			StopRenaming(false);
			_open = false;
			_pinned = false;
			_hovered = 0;

			if (_fader != null)
			{
				_fader.Hide();
			}

			RestoreGameInput();
		}

		/// <summary>
		/// Drives the wheel. Returns true while it wants the mod's other keys left
		/// alone, which is any time it's open.
		/// </summary>
		public bool Tick(bool radialHeld, bool radialReleased, bool radialPressed)
		{
			if (!_open)
			{
				return false;
			}

			// The input field owns the keyboard and the mouse while it's up; it tells
			// us when it's finished through onEndEdit.
			if (_renaming)
			{
				return true;
			}

			UpdateHover();

			// A second press of the wheel key always closes, whether pinned or not.
			if (radialPressed)
			{
				Cancel();
				return true;
			}

			// Until it's pinned the wheel lives and dies with the key. Testing "not
			// held" as well as "released this frame" matters: a tap quick enough to
			// press and release inside one frame is opened and released on separate
			// frames, so the release event has already been and gone by the time we
			// first look for it.
			bool heldPastTapThreshold = Time.unscaledTime - _openedAt >= Mathf.Max(_config.RadialTapSeconds, 0f);

			if (!_pinned && (radialReleased || !radialHeld))
			{
				if (!heldPastTapThreshold)
				{
					// Too quick to have been a hold: leave it up to be clicked.
					_pinned = true;
					DecideMode();
				}
				else
				{
					ActivateOrCancel();
				}

				return true;
			}

			// Still on the key past the threshold, so it's a hold after all.
			if (!_modeDecided && heldPastTapThreshold)
			{
				DecideMode();
			}

			TickActionKeys();
			return true;
		}

		/// <summary>
		/// Puts the cursor in the wheel's dead zone as it opens. Without this the
		/// wheel is centred on the screen but the cursor isn't, so holding the key
		/// and letting go without moving would fly you to whichever place the cursor
		/// happened to be pointing at - and "don't move" has to mean "never mind".
		/// </summary>
		private static void CentreCursor()
		{
			Mouse mouse = Mouse.current;
			if (mouse != null)
			{
				mouse.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
			}
		}

		private void ActivateOrCancel()
		{
			if (_hovered <= 0)
			{
				Cancel();
				return;
			}

			// Ctrl means "overwrite" however the slot was picked, held or clicked.
			Keyboard keyboard = Keyboard.current;
			Activate(_hovered, keyboard != null && keyboard.ctrlKey.isPressed);
		}

		/// <summary>
		/// Tap or hold is now settled, so the prompt has something true to say.
		/// Nothing else would redraw it until the hovered slot happened to change.
		/// </summary>
		private void DecideMode()
		{
			_modeDecided = true;
			RefreshLabels();
		}

		private void Cancel()
		{
			NativeUi.Sfx(AudioEvent.UIButtonClose);
			Close();
		}

		private void TickActionKeys()
		{
			Mouse mouse = Mouse.current;
			Keyboard keyboard = Keyboard.current;

			// A click that dismissed the rename field was consumed by doing so.
			if (Time.frameCount == _renameEndedFrame)
			{
				return;
			}

			if (mouse != null && mouse.leftButton.wasPressedThisFrame)
			{
				bool overwrite = keyboard != null && keyboard.ctrlKey.isPressed;
				if (_hovered > 0)
				{
					Activate(_hovered, overwrite);
				}
				else
				{
					Cancel();
				}

				return;
			}

			if (mouse != null && mouse.rightButton.wasPressedThisFrame)
			{
				Cancel();
				return;
			}

			if (keyboard == null)
			{
				return;
			}

			// The game's own abort handler runs before this and, with player input
			// disabled, only raises an event; closing here keeps Escape meaning
			// "close the wheel".
			if (keyboard.escapeKey.wasPressedThisFrame)
			{
				Cancel();
				return;
			}

			if (_hovered <= 0)
			{
				return;
			}

			// Everything below acts on a saved location; an empty slot has nothing to
			// rename, clear or photograph.
			if (!_actions.TryGetLocation(_hovered, out SavedLocation _))
			{
				return;
			}

			if (keyboard.f2Key.wasPressedThisFrame)
			{
				StartRenaming(_hovered);
				return;
			}

			if (keyboard.deleteKey.wasPressedThisFrame)
			{
				NativeUi.Sfx(AudioEvent.UIButtonClickHeavy);
				_actions.ClearSlot(_hovered);
				Refresh();
				return;
			}

			if (keyboard.rKey.wasPressedThisFrame)
			{
				NativeUi.Sfx(AudioEvent.UIButtonClick);

				int slot = _hovered;
				Close();
				_actions.RefreshPreview(slot);
			}
		}

		private void Activate(int slot, bool overwrite)
		{
			bool filled = _actions.TryGetLocation(slot, out SavedLocation _);

			if (overwrite || !filled)
			{
				NativeUi.Sfx(AudioEvent.UIButtonClickHeavy);
				_actions.BindSlot(slot);
				Refresh();

				// Saving is the sort of thing you do several of at once, so a pinned
				// wheel stays up; a held one was a single deliberate action.
				if (!_pinned)
				{
					Close();
				}

				return;
			}

			NativeUi.Sfx(AudioEvent.UIButtonClick);

			// Close first: that unlocks the camera before the glide starts, rather
			// than leaving CamOperator briefly running its own smoothing against a
			// target still pointed at where we came from.
			Close();
			_actions.JumpToSlot(slot);
		}

		/// <summary>
		/// Works in the ring's own coordinates rather than screen pixels, so the UI
		/// scale setting and the canvas scaler are already accounted for.
		/// </summary>
		private void UpdateHover()
		{
			Mouse mouse = Mouse.current;
			if (mouse == null)
			{
				return;
			}

			int previous = _hovered;
			_hovered = 0;

			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
				_ring, mouse.position.ReadValue(), null, out Vector2 local))
			{
				// Undo the layout's horizontal stretch, which maps the ellipse the
				// slots sit on back onto a circle. The angle then lands exactly on the
				// slot it looks like it should, and the dead zone stays round.
				Vector2 circular = new Vector2(local.x / HorizontalStretch, local.y);
				if (circular.magnitude >= DeadZoneRadius)
				{
					// Layout runs clockwise from twelve o'clock, so the angle does too.
					float degrees = Mathf.Atan2(circular.x, circular.y) * Mathf.Rad2Deg;
					if (degrees < 0f)
					{
						degrees += 360f;
					}

					float step = 360f / SlotCount;
					_hovered = (Mathf.RoundToInt(degrees / step) % SlotCount) + 1;
				}
			}

			if (_hovered != previous)
			{
				if (_hovered > 0)
				{
					NativeUi.Sfx(AudioEvent.Hover);
				}

				RefreshHighlight();
			}
		}

		/// <summary>
		/// Hands editing over to a real TMP_InputField, which brings the caret,
		/// click-and-drag selection, shift and ctrl word jumps, ctrl+backspace and
		/// the clipboard with it. The game uses these itself in a dozen places, so
		/// keyboard input reaches them whatever the input backend is doing.
		/// </summary>
		private void StartRenaming(int slot)
		{
			if (_renameField == null)
			{
				return;
			}

			_actions.TryGetLocation(slot, out SavedLocation location);

			_renaming = true;
			_renamingSlot = slot;

			// Renaming settles it: you're editing, not mid-gesture.
			_pinned = true;
			_modeDecided = true;

			_centreLabel.enabled = false;
			_renameField.gameObject.SetActive(true);
			_renameField.text = (location == null || location.Label == null) ? string.Empty : location.Label;

			// Activation is deferred to the field's next Update, so setting the caret
			// or selection here would just be overwritten. onFocusSelectAll - on by
			// default - selects the old name for us, so typing replaces it.
			_renameField.ActivateInputField();

			if (EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(_renameField.gameObject);
			}

			NativeUi.Sfx(AudioEvent.UIButtonClickGentle);
			RefreshLabels();
		}

		/// <summary>
		/// TMP_InputField raises this for Enter, for Escape and for clicking away.
		/// Escape sets wasCanceled and restores the original text on its own.
		/// </summary>
		private void OnRenameEnded(string value)
		{
			StopRenaming(_renameField == null || !_renameField.wasCanceled, value);
		}

		private void StopRenaming(bool commit)
		{
			StopRenaming(commit, (_renameField == null) ? null : _renameField.text);
		}

		private void StopRenaming(bool commit, string value)
		{
			if (!_renaming)
			{
				return;
			}

			// Set first: deactivating the field raises onEndEdit, which lands back here.
			_renaming = false;
			_renameEndedFrame = Time.frameCount;

			if (_renameField != null)
			{
				_renameField.DeactivateInputField();
				_renameField.gameObject.SetActive(false);
			}

			if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
			{
				EventSystem.current.SetSelectedGameObject(null);
			}

			_centreLabel.enabled = true;

			if (commit)
			{
				_actions.RenameSlot(_renamingSlot, (value ?? string.Empty).Trim());
				NativeUi.Sfx(AudioEvent.UIButtonClick);
			}

			Refresh();
		}

		private void Refresh()
		{
			if (_root == null)
			{
				return;
			}

			for (int slot = 1; slot <= SlotCount; slot++)
			{
				Wedge wedge = _wedges[slot - 1];
				bool filled = _actions.TryGetLocation(slot, out SavedLocation _);
				Texture2D preview = _config.ShowRadialPreviews ? _actions.PreviewFor(slot) : null;

				wedge.Preview.texture = preview;
				wedge.Preview.enabled = preview != null;

				// A saved place with no photo yet still needs to read as occupied.
				wedge.Placeholder.enabled = preview == null;
				wedge.Placeholder.text = filled ? slot.ToString() : "+";
				wedge.Placeholder.color = filled ? UIStyle.gold : new Color(1f, 1f, 1f, 0.25f);

				wedge.KeyCap.text = _actions.SlotKeyLabel(slot);
			}

			RefreshHighlight();
		}

		private void RefreshHighlight()
		{
			for (int slot = 1; slot <= SlotCount; slot++)
			{
				Wedge wedge = _wedges[slot - 1];
				bool filled = _actions.TryGetLocation(slot, out SavedLocation _);
				bool hovered = slot == _hovered;

				// The plate stays dark so photos keep their contrast; it's the frame
				// that lights up.
				wedge.Plate.color = filled ? NativeUi.PanelBackground : NativeUi.PanelBackgroundDim;
				wedge.Border.color = hovered ? UIStyle.warmWhite : NativeUi.PanelBorder;
				wedge.Root.localScale = hovered ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
			}

			RefreshLabels();
		}

		private void RefreshLabels()
		{
			if (_renaming)
			{
				SetLegend("Enter: Save  ·  Escape: Cancel");
				return;
			}

			_centreLabel.color = UIStyle.gold;

			SavedLocation location = null;
			bool filled = _hovered > 0 && _actions.TryGetLocation(_hovered, out location);
			_centreLabel.text = (_hovered <= 0) ? string.Empty : NameFor(_hovered, location);

			// Tap and hold want opposite prompts, and for the first moment either is
			// still possible. The name in the middle carries the feedback until we
			// know which one this is.
			if (!_modeDecided)
			{
				SetLegend(string.Empty);
				return;
			}

			// Holding the key is one gesture, and every action below it needs a free
			// mouse hand. Listing them while the wheel is still on the key would only
			// offer things you can't reach, so the prompt stays put and just the name
			// in the middle follows the cursor.
			if (!_pinned)
			{
				SetLegend("Select a location, then release");
				return;
			}

			if (_hovered <= 0)
			{
				SetLegend("Click to select a location.");
				return;
			}

			SetLegend(filled
				? "Click: Go  ·  Ctrl+Click: Save this view here  ·  F2: Rename  ·  Del: Clear  ·  R: Refresh preview"
				: "Click: Save this view here");
		}

		/// <summary>Keeps the legend's plate wrapped to whatever it's currently saying.</summary>
		private void SetLegend(string text)
		{
			// No text means no plate either, rather than an empty bordered box.
			bool visible = !string.IsNullOrEmpty(text);
			if (_legendPanel.gameObject.activeSelf != visible)
			{
				_legendPanel.gameObject.SetActive(visible);
			}

			if (!visible || _legend.text == text)
			{
				return;
			}

			_legend.text = text;

			Vector2 preferred = _legend.GetPreferredValues(text);
			_legendPanel.sizeDelta = new Vector2(preferred.x + 44f, preferred.y + 24f);
		}

		private static string NameFor(int slot, SavedLocation location)
		{
			return (location == null) ? "Empty " + slot : location.DisplayName(slot);
		}

		/// <summary>
		/// The wheel is a modal: the scrim stops the tabletop taking clicks (tokens
		/// don't consult the input flag), and DisablePlayerInput stops camera panning,
		/// zooming and every base-game hotkey. Zero seconds means "until we say so".
		/// </summary>
		private void DisableGameInput()
		{
			LocalNexus nexus = Watchman.Get<LocalNexus>();
			if (nexus == null || _inputDisabledByUs)
			{
				return;
			}

			nexus.DisablePlayerInput(0f);
			_inputDisabledByUs = true;
		}

		private void RestoreGameInput()
		{
			if (!_inputDisabledByUs)
			{
				return;
			}

			// Clear the flag first: if this throws, we must not try again forever.
			_inputDisabledByUs = false;

			LocalNexus nexus = Watchman.Get<LocalNexus>();
			if (nexus != null)
			{
				nexus.EnablePlayerInput();
			}
		}

		public void Destroy()
		{
			// Never leave the game with its input switched off.
			StopRenaming(false);
			RestoreGameInput();

			if (_root != null)
			{
				UnityEngine.Object.Destroy(_root.gameObject);
				_root = null;
			}
		}

		/// <summary>Builds the wheel on first open. False if the canvas isn't up yet.</summary>
		private bool Build()
		{
			if (_root != null)
			{
				return true;
			}

			Transform parent = NativeUi.GetOverlayParent();
			if (parent == null)
			{
				return false;
			}

			_root = NativeUi.MakeRect(parent, "LocationHotkeysWheel");
			NativeUi.Fill(_root);

			// Sibling order alone isn't enough: the pop-out panels for verbs and desk
			// actions live on canvases of their own and would draw over us. Sorting
			// order settles it across canvases, and the game never sets one anywhere,
			// so a high value can't collide with anything of the game's own.
			NativeUi.SortAbove(_root, NativeUi.SortOrderWheel);

			Image scrim = _root.gameObject.AddComponent<Image>();
			scrim.color = NativeUi.Scrim;
			// The one thing on the wheel that takes raycasts, so the tabletop can't.
			scrim.raycastTarget = true;

			_root.gameObject.AddComponent<CanvasGroup>();
			_fader = _root.gameObject.AddComponent<UnscaledFader>();

			_ring = NativeUi.MakeRect(_root, "Ring");
			NativeUi.Anchor(_ring, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

			float radius = Mathf.Max(_config.RadialRadius, DeadZoneRadius + WedgeHeight);
			float hubSize = DeadZoneRadius * 2f;

			Image hub = NativeUi.MakeImage(_ring, "Hub", NativeUi.Disc(256), NativeUi.PanelBackground);
			NativeUi.Anchor(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(hubSize, hubSize));

			Image hubRing = NativeUi.MakeImage(_ring, "HubRing", NativeUi.Ring(256, 3f), NativeUi.PanelBorder);
			NativeUi.Anchor(hubRing.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(hubSize, hubSize));

			_wedges = new Wedge[SlotCount];
			float step = 360f / SlotCount;
			for (int slot = 1; slot <= SlotCount; slot++)
			{
				float radians = (slot - 1) * step * Mathf.Deg2Rad;
				Vector2 position = new Vector2(
					Mathf.Sin(radians) * radius * HorizontalStretch,
					Mathf.Cos(radians) * radius);
				_wedges[slot - 1] = BuildWedge(slot, position);
			}

			// Sits inside the hub and shrinks to fit rather than spilling out of it.
			_centreLabel = NativeUi.MakeText(_ring, "CentreLabel", LanguageManager.eFontStyle.Heading, 26f, UIStyle.gold);
			NativeUi.Anchor(_centreLabel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(hubSize - 34f, hubSize - 34f));
			_centreLabel.alignment = TextAlignmentOptions.Center;
			_centreLabel.enableWordWrapping = true;
			_centreLabel.enableAutoSizing = true;
			_centreLabel.fontSizeMin = 13f;
			_centreLabel.fontSizeMax = 28f;
			_centreLabel.overflowMode = TextOverflowModes.Ellipsis;

			_renameField = BuildRenameField(hubSize);

			NativeUi.Panel legendPanel = NativeUi.MakePanel(_root, "LegendPanel", 12, 2f);
			// Clear of the bottom slot's key cap, which hangs below its plate.
			NativeUi.Anchor(legendPanel.Root, new Vector2(0.5f, 0.5f), new Vector2(0f, -(radius + 128f)), new Vector2(600f, 44f));
			_legendPanel = legendPanel.Root;

			_legend = NativeUi.MakeText(legendPanel.Root, "Legend", LanguageManager.eFontStyle.BodyText, 17f, UIStyle.gold);
			NativeUi.Fill(_legend.rectTransform);
			legendPanel.RaiseBorder();

			_root.gameObject.SetActive(false);
			return true;
		}

		/// <summary>
		/// A TMP_InputField wired up by hand. It has to be built inactive: the
		/// component initialises in OnEnable, and it needs its viewport and text
		/// component already assigned by then or the caret never appears.
		/// </summary>
		private TMP_InputField BuildRenameField(float hubSize)
		{
			RectTransform root = NativeUi.MakeRect(_ring, "RenameField");
			NativeUi.Anchor(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(hubSize - 30f, 46f));
			root.gameObject.SetActive(false);

			RectTransform viewport = NativeUi.MakeRect(root, "TextViewport");
			NativeUi.Fill(viewport);
			viewport.gameObject.AddComponent<RectMask2D>();

			TextMeshProUGUI text = NativeUi.MakeText(viewport, "Text", LanguageManager.eFontStyle.Heading, 22f, UIStyle.warmWhite);
			NativeUi.Fill(text.rectTransform);
			text.enableWordWrapping = false;
			text.raycastTarget = true;

			// The field needs a graphic of its own to be clickable at all.
			Image background = root.gameObject.AddComponent<Image>();
			background.sprite = NativeUi.RoundedRect(10);
			background.type = Image.Type.Sliced;
			background.color = new Color(0f, 0f, 0f, 0.5f);

			TMP_InputField field = root.gameObject.AddComponent<TMP_InputField>();
			field.textViewport = viewport;
			field.textComponent = text;
			field.fontAsset = text.font;
			field.pointSize = 22f;
			field.lineType = TMP_InputField.LineType.SingleLine;
			field.characterLimit = NameLengthLimit;
			field.richText = false;
			field.restoreOriginalTextOnEscape = true;
			field.onFocusSelectAll = true;
			field.customCaretColor = true;
			field.caretColor = UIStyle.warmWhite;
			field.caretWidth = 2;
			field.selectionColor = new Color(UIStyle.gold.r, UIStyle.gold.g, UIStyle.gold.b, 0.35f);
			field.onEndEdit.AddListener(OnRenameEnded);

			return field;
		}

		private Wedge BuildWedge(int slot, Vector2 position)
		{
			Wedge wedge = new Wedge();

			wedge.Root = NativeUi.MakeRect(_ring, "Wedge" + slot);
			NativeUi.Anchor(wedge.Root, new Vector2(0.5f, 0.5f), position, new Vector2(WedgeWidth, WedgeHeight));

			NativeUi.Panel panel = NativeUi.MakePanel(wedge.Root, "Plate", 12, 2f);
			NativeUi.Fill(panel.Root);
			wedge.Plate = panel.Fill;
			wedge.Border = panel.Border;

			// The photo is clipped to the plate's own curve rather than squaring off
			// inside it. Radius 8 against the plate's 12 keeps the inset even round
			// the corners as well as along the sides.
			RectTransform previewMask = NativeUi.MakeMask(panel.Root, "PreviewMask", 8);
			NativeUi.Fill(previewMask);
			previewMask.offsetMin = new Vector2(4f, 4f);
			previewMask.offsetMax = new Vector2(-4f, -4f);

			RectTransform previewRect = NativeUi.MakeRect(previewMask, "Preview");
			NativeUi.Fill(previewRect);
			wedge.Preview = previewRect.gameObject.AddComponent<RawImage>();
			wedge.Preview.raycastTarget = false;

			wedge.Placeholder = NativeUi.MakeText(panel.Root, "Placeholder", LanguageManager.eFontStyle.Heading, 30f, UIStyle.gold);
			NativeUi.Fill(wedge.Placeholder.rectTransform);

			// The frame goes over the photo, so it reads as a mount rather than a backing.
			panel.RaiseBorder();

			// Outside the plate: on top of a photo a key cap is unreadable, and the
			// ellipse leaves plenty of room underneath.
			wedge.KeyCap = NativeUi.MakeText(wedge.Root, "KeyCap", LanguageManager.eFontStyle.Numbers, 15f, UIStyle.gold);
			NativeUi.Anchor(wedge.KeyCap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -15f), new Vector2(WedgeWidth, 24f));

			return wedge;
		}
	}
}
