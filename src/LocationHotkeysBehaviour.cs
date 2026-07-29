using System;
using System.Collections.Generic;
using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>
	/// Watches the mod's keybindings once the playfield is up: binds the current
	/// camera position to a slot, and flies the camera back to it on demand.
	/// </summary>
	public class LocationHotkeysBehaviour : MonoBehaviour, ILocationActions
	{
		private const float ToastSeconds = 2f;

		/// <summary>Give up rather than spam the log if something is structurally wrong.</summary>
		private const int MaxConsecutiveErrors = 10;

		/// <summary>How long to wait for the game's input before saying so in the log.</summary>
		private const float AttachWarningSeconds = 60f;

		private bool _attachWarningLogged;

		private HotkeyConfig _config;

		private readonly GameBindings _bindings = new GameBindings();

		private KeyNames.Modifier _quickBindModifier;

		private KeyNames.Modifier _clearModifier;

		private bool _bindMode;

		private float _bindModeExpiresAt;

		private int _consecutiveErrors;

		private readonly ToastBanner _banner = new ToastBanner();

		private LocationPreviews _previews;

		private RadialMenu _radial;

		public void Awake()
		{
			_config = HotkeyConfig.Load();
			_quickBindModifier = KeyNames.ParseModifier(_config.QuickBindModifier);
			_clearModifier = KeyNames.ParseModifier(_config.ClearModifier);
			_previews = new LocationPreviews(_config, this);
			_radial = new RadialMenu(_config, this);
			NoonUtility.Log("Location Hotkeys: config at " + HotkeyConfig.Path);
		}

		public void Update()
		{
			try
			{
				Tick();
				_consecutiveErrors = 0;
			}
			catch (Exception e)
			{
				_consecutiveErrors++;
				NoonUtility.LogWarning("Location Hotkeys: " + e);
				if (_consecutiveErrors >= MaxConsecutiveErrors)
				{
					NoonUtility.LogWarning("Location Hotkeys: too many consecutive errors; disabling the mod for this session.");
					enabled = false;
				}
			}
		}

		private void Tick()
		{
			// Keeps timed toasts fading out even once we've left the playfield.
			_banner.Tick();

			// Register with the game's keybindings as soon as ControlsController
			// exists, which is well before any playfield: Options -> Controls is
			// reachable from the main menu, and the rows there read these actions.
			bool registered = _bindings.TryAttach();
			if (!registered && !_attachWarningLogged && Time.unscaledTime > AttachWarningSeconds)
			{
				_attachWarningLogged = true;
				NoonUtility.LogWarning("Location Hotkeys: no ControlsController after " + AttachWarningSeconds + "s, so the keys were never registered - the Options > Controls rows will read blank and the hotkeys won't fire.");
			}

			// Outside the playfield - main menu, loading, the debug console, a text
			// field - the slot keys aren't ours to read. The wheel is the exception:
			// renaming focuses a text field of our own, and nothing else can be
			// taking focus while the wheel is holding the game still.
			if (!registered || !PlayfieldIsActive() || Watchman.DebugIsVisible()
				|| (TextEntryHasFocus() && !_radial.IsOpen))
			{
				SetBindMode(false);
				_radial.Close();
				return;
			}

			// The wheel is modal: while it's up it owns the mouse and the keyboard,
			// including our own slot keys.
			bool radialPressed = _bindings.WasPressedThisFrame(GameBindings.RadialAction);
			if (_radial.Tick(
				_bindings.IsHeld(GameBindings.RadialAction),
				_bindings.WasReleasedThisFrame(GameBindings.RadialAction),
				radialPressed))
			{
				return;
			}

			if (radialPressed)
			{
				SetBindMode(false);
				_radial.Open();
				return;
			}

			if (_bindMode && Time.unscaledTime > _bindModeExpiresAt)
			{
				SetBindMode(false);
				Toast("Bind mode cancelled.");
			}

			if (_bindings.WasPressedThisFrame(GameBindings.BindModeAction))
			{
				ToggleBindMode();
				return;
			}

			for (int slot = 1; slot <= GameBindings.SlotCount; slot++)
			{
				if (!_bindings.WasPressedThisFrame(GameBindings.SlotAction(slot)))
				{
					continue;
				}

				if (_bindMode)
				{
					SetBindMode(false);
					if (ModifierHeld(_clearModifier))
					{
						Clear(slot);
					}
					else
					{
						Bind(slot);
					}
				}
				else if (_quickBindModifier != KeyNames.Modifier.None && ModifierHeld(_quickBindModifier))
				{
					Bind(slot);
				}
				else
				{
					Jump(slot);
				}

				return;
			}
		}

		private static bool ModifierHeld(KeyNames.Modifier modifier)
		{
			Keyboard keyboard = Keyboard.current;
			return keyboard != null && KeyNames.IsHeld(keyboard, modifier);
		}

		private void ToggleBindMode()
		{
			if (_bindMode)
			{
				SetBindMode(false);
				Toast("Bind mode cancelled.");
			}
			else
			{
				SetBindMode(true);
			}
		}

		/// <summary>Single place bind mode flips, so the banner can't drift out of step with it.</summary>
		private void SetBindMode(bool on)
		{
			if (on == _bindMode)
			{
				return;
			}

			_bindMode = on;
			if (on)
			{
				_bindModeExpiresAt = Time.unscaledTime + Mathf.Max(_config.BindModeTimeoutSeconds, 1f);
				if (_config.ShowOverlay)
				{
					_banner.ShowPersistent(BindModePrompt());
				}
			}
			else
			{
				_banner.Hide();
			}
		}

		private void Bind(int slot)
		{
			if (!Playthrough.TryGetCurrent(out string playthroughId, out string label))
			{
				Toast("No playthrough to save this against.");
				return;
			}

			CamOperator cam = Watchman.Get<CamOperator>();
			Camera attached = (cam == null) ? null : cam.GetAttachedCamera();
			if (attached == null)
			{
				Toast("Can't read the camera right now.");
				return;
			}

			string slotAction = GameBindings.SlotAction(slot);

			// Rebinding a slot moves the view, not the name the player gave the place.
			_config.TryGetLocation(playthroughId, slotAction, out SavedLocation existing);

			Vector3 position = attached.transform.position;
			_config.SetLocation(playthroughId, label, slotAction, new SavedLocation
			{
				X = position.x,
				Y = position.y,
				Z = position.z,
				Label = (existing == null) ? null : existing.Label
			});

			// The camera is already here and the scene is already in the right zoom
			// state for this height, so this is the one moment the thumbnail is honest.
			_previews.Capture(playthroughId, slotAction);

			Toast("Saved this view to " + SlotLabel(slot) + ".");
		}

		private void Clear(int slot)
		{
			if (!Playthrough.TryGetCurrent(out string playthroughId, out string _))
			{
				Toast("No playthrough to clear this from.");
				return;
			}

			string slotAction = GameBindings.SlotAction(slot);
			if (_config.ClearLocation(playthroughId, slotAction))
			{
				_previews.Delete(playthroughId, slotAction);
				Toast("Cleared " + SlotLabel(slot) + ".");
			}
			else
			{
				Toast(SlotLabel(slot) + " was already empty.");
			}
		}

		private void Jump(int slot)
		{
			if (!Playthrough.TryGetCurrent(out string playthroughId, out string _)
				|| !_config.TryGetLocation(playthroughId, GameBindings.SlotAction(slot), out SavedLocation location))
			{
				return;
			}

			CamOperator cam = Watchman.Get<CamOperator>();
			if (cam == null)
			{
				Toast("Can't move the camera right now.");
				return;
			}

			// Kill any drag drift or held-key panning first, or it fights the glide.
			cam.StopAllMovement();

			float height = _config.RestoreZoom ? location.Z : cam.GetCurrentZoomHeight();
			cam.PointAtTableLevelAtHeight(location.TablePosition, height, _config.ClampedTravelSeconds, null);
		}

		/// <summary>Whatever key the player has this slot bound to right now.</summary>
		private string SlotLabel(int slot)
		{
			return _bindings.DisplayString(GameBindings.SlotAction(slot));
		}

		private static bool PlayfieldIsActive()
		{
			StageHand stageHand = Watchman.Get<StageHand>();
			Compendium compendium = Watchman.Get<Compendium>();
			if (stageHand == null || compendium == null)
			{
				return false;
			}

			Dictum dictum = compendium.GetSingleEntity<Dictum>();
			if (dictum == null || string.IsNullOrEmpty(dictum.PlayfieldScene))
			{
				return false;
			}

			return stageHand.SceneIsActive(dictum.PlayfieldScene);
		}

		private static bool TextEntryHasFocus()
		{
			EventSystem eventSystem = EventSystem.current;
			GameObject selected = (eventSystem == null) ? null : eventSystem.currentSelectedGameObject;
			if (selected == null)
			{
				return false;
			}

			TMP_InputField tmpField = selected.GetComponent<TMP_InputField>();
			if (tmpField != null && tmpField.isFocused)
			{
				return true;
			}

			InputField legacyField = selected.GetComponent<InputField>();
			return legacyField != null && legacyField.isFocused;
		}

		private void Toast(string message)
		{
			if (_config != null && _config.ShowOverlay)
			{
				_banner.Show(message, ToastSeconds);
			}
		}

		private string BindModePrompt()
		{
			List<string> keys = new List<string>();
			for (int slot = 1; slot <= GameBindings.SlotCount; slot++)
			{
				keys.Add(SlotLabel(slot));
			}

			string line = "BIND MODE - press " + string.Join(" ", keys.ToArray()) + " to save this view";
			if (_clearModifier != KeyNames.Modifier.None)
			{
				line += "  |  " + _clearModifier + "+key clears it";
			}

			return line;
		}

		// --- ILocationActions: what the wheel is allowed to ask of us. ---

		string ILocationActions.SlotKeyLabel(int slot)
		{
			return SlotLabel(slot);
		}

		bool ILocationActions.TryGetLocation(int slot, out SavedLocation location)
		{
			location = null;
			return Playthrough.TryGetCurrent(out string playthroughId, out string _)
				&& _config.TryGetLocation(playthroughId, GameBindings.SlotAction(slot), out location);
		}

		Texture2D ILocationActions.PreviewFor(int slot)
		{
			if (!Playthrough.TryGetCurrent(out string playthroughId, out string _))
			{
				return null;
			}

			return _previews.Get(playthroughId, GameBindings.SlotAction(slot));
		}

		void ILocationActions.JumpToSlot(int slot)
		{
			Jump(slot);
		}

		void ILocationActions.BindSlot(int slot)
		{
			Bind(slot);
		}

		void ILocationActions.ClearSlot(int slot)
		{
			Clear(slot);
		}

		void ILocationActions.RefreshPreview(int slot)
		{
			string slotAction = GameBindings.SlotAction(slot);
			if (!Playthrough.TryGetCurrent(out string playthroughId, out string _)
				|| !_config.TryGetLocation(playthroughId, slotAction, out SavedLocation location))
			{
				return;
			}

			_previews.RefreshOnArrival(playthroughId, slotAction, location);
			Toast("Taking a new photo of " + location.DisplayName(slot) + ".");
		}

		void ILocationActions.RenameSlot(int slot, string name)
		{
			if (!Playthrough.TryGetCurrent(out string playthroughId, out string _)
				|| !_config.TryGetLocation(playthroughId, GameBindings.SlotAction(slot), out SavedLocation location))
			{
				return;
			}

			// TryGetLocation hands back the stored object, so this is the stored name.
			location.Label = string.IsNullOrEmpty(name) ? null : name;
			_config.Save();
		}

		public void OnDestroy()
		{
			_banner.Destroy();
			_radial.Destroy();
			_previews.Destroy();
		}
	}
}
