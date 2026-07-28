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
	public class LocationHotkeysBehaviour : MonoBehaviour
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

		private string _toast;

		private float _toastExpiresAt;

		private int _consecutiveErrors;

		private Texture2D _panelTexture;

		private GUIStyle _panelStyle;

		public void Awake()
		{
			_config = HotkeyConfig.Load();
			_quickBindModifier = KeyNames.ParseModifier(_config.QuickBindModifier);
			_clearModifier = KeyNames.ParseModifier(_config.ClearModifier);
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
			// field - the slot keys aren't ours to read.
			if (!registered || !PlayfieldIsActive() || Watchman.DebugIsVisible() || TextEntryHasFocus())
			{
				_bindMode = false;
				return;
			}

			if (_bindMode && Time.unscaledTime > _bindModeExpiresAt)
			{
				_bindMode = false;
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
					_bindMode = false;
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
			_bindMode = !_bindMode;
			if (_bindMode)
			{
				_bindModeExpiresAt = Time.unscaledTime + Mathf.Max(_config.BindModeTimeoutSeconds, 1f);
			}
			else
			{
				Toast("Bind mode cancelled.");
			}
		}

		private void Bind(int slot)
		{
			CamOperator cam = Watchman.Get<CamOperator>();
			Camera attached = (cam == null) ? null : cam.GetAttachedCamera();
			if (attached == null)
			{
				Toast("Can't read the camera right now.");
				return;
			}

			Vector3 position = attached.transform.position;
			_config.Locations[GameBindings.SlotAction(slot)] = new SavedLocation
			{
				X = position.x,
				Y = position.y,
				Z = position.z
			};
			_config.Save();

			Toast("Saved this view to " + SlotLabel(slot) + ".");
		}

		private void Clear(int slot)
		{
			if (_config.Locations.Remove(GameBindings.SlotAction(slot)))
			{
				_config.Save();
				Toast("Cleared " + SlotLabel(slot) + ".");
			}
			else
			{
				Toast(SlotLabel(slot) + " was already empty.");
			}
		}

		private void Jump(int slot)
		{
			if (!_config.Locations.TryGetValue(GameBindings.SlotAction(slot), out SavedLocation location) || location == null)
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
			_toast = message;
			_toastExpiresAt = Time.unscaledTime + ToastSeconds;
		}

		public void OnGUI()
		{
			if (_config == null || !_config.ShowOverlay)
			{
				return;
			}

			if (_bindMode)
			{
				DrawPanel(BindModePrompt());
			}
			else if (_toast != null && Time.unscaledTime < _toastExpiresAt)
			{
				DrawPanel(_toast);
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

		private void DrawPanel(string text)
		{
			EnsureStyle();

			Vector2 size = _panelStyle.CalcSize(new GUIContent(text));
			float width = Mathf.Min(size.x + 32f, Screen.width - 40f);
			float height = size.y + 20f;
			Rect rect = new Rect((Screen.width - width) / 2f, Screen.height - height - 48f, width, height);

			GUI.Box(rect, GUIContent.none, _panelStyle);
			GUI.Label(rect, text, _panelStyle);
		}

		private void EnsureStyle()
		{
			// GUI styles can't be built in Awake - OnGUI is the first point GUI.skin exists.
			if (_panelStyle != null && _panelTexture != null)
			{
				return;
			}

			_panelTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
			_panelTexture.SetPixel(0, 0, new Color(0.05f, 0.04f, 0.03f, 0.88f));
			_panelTexture.Apply();

			_panelStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 16,
				wordWrap = false,
				padding = new RectOffset(16, 16, 10, 10)
			};
			_panelStyle.normal.background = _panelTexture;
			_panelStyle.normal.textColor = new Color(0.93f, 0.87f, 0.72f);
		}

		public void OnDestroy()
		{
			if (_panelTexture != null)
			{
				Destroy(_panelTexture);
			}
		}
	}
}
