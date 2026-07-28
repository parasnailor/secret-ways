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
	/// Polls the keyboard once the playfield is up: binds the current camera
	/// position to a slot key, and flies the camera back to it on demand.
	/// </summary>
	public class LocationHotkeysBehaviour : MonoBehaviour
	{
		private const float ToastSeconds = 2f;

		/// <summary>Give up rather than spam the log if something is structurally wrong.</summary>
		private const int MaxConsecutiveErrors = 10;

		private struct Slot
		{
			public string Name;

			public Key Key;
		}

		private HotkeyConfig _config;

		private readonly List<Slot> _slots = new List<Slot>();

		private Key _bindModeKey;

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

			if (!KeyNames.TryParseKey(_config.BindModeKey, out _bindModeKey))
			{
				NoonUtility.LogWarning("Location Hotkeys: '" + _config.BindModeKey + "' isn't a key name, so bind mode has no key. Valid names are the UnityEngine.InputSystem.Key values, e.g. PageUp, Home, Backslash, Semicolon.");
			}

			_quickBindModifier = KeyNames.ParseModifier(_config.QuickBindModifier);
			_clearModifier = KeyNames.ParseModifier(_config.ClearModifier);

			foreach (string name in _config.SlotKeys)
			{
				if (KeyNames.TryParseKey(name, out Key key))
				{
					_slots.Add(new Slot { Name = name, Key = key });
				}
				else
				{
					NoonUtility.LogWarning("Location Hotkeys: skipping slot key '" + name + "' - not a recognised key name.");
				}
			}

			NoonUtility.Log("Location Hotkeys: watching " + _slots.Count + " slot key(s); config at " + HotkeyConfig.Path);
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
			Keyboard keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return;
			}

			// Outside the playfield - main menu, loading, the debug console, a text
			// field - the slot keys aren't ours to read.
			if (!PlayfieldIsActive() || Watchman.DebugIsVisible() || TextEntryHasFocus())
			{
				_bindMode = false;
				return;
			}

			if (_bindMode && Time.unscaledTime > _bindModeExpiresAt)
			{
				_bindMode = false;
				Toast("Bind mode cancelled.");
			}

			if (KeyNames.WasPressedThisFrame(keyboard, _bindModeKey))
			{
				ToggleBindMode();
				return;
			}

			foreach (Slot slot in _slots)
			{
				if (!KeyNames.WasPressedThisFrame(keyboard, slot.Key))
				{
					continue;
				}

				if (_bindMode)
				{
					_bindMode = false;
					if (KeyNames.IsHeld(keyboard, _clearModifier))
					{
						Clear(slot);
					}
					else
					{
						Bind(slot);
					}
				}
				else if (_quickBindModifier != KeyNames.Modifier.None && KeyNames.IsHeld(keyboard, _quickBindModifier))
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

		private void ToggleBindMode()
		{
			if (_slots.Count == 0)
			{
				Toast("No slot keys are configured.");
				return;
			}

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

		private void Bind(Slot slot)
		{
			CamOperator cam = Watchman.Get<CamOperator>();
			Camera attached = (cam == null) ? null : cam.GetAttachedCamera();
			if (attached == null)
			{
				Toast("Can't read the camera right now.");
				return;
			}

			Vector3 position = attached.transform.position;
			_config.Locations[slot.Name] = new SavedLocation
			{
				X = position.x,
				Y = position.y,
				Z = position.z
			};
			_config.Save();

			Toast("Saved this view to " + slot.Name + ".");
		}

		private void Clear(Slot slot)
		{
			if (_config.Locations.Remove(slot.Name))
			{
				_config.Save();
				Toast("Cleared " + slot.Name + ".");
			}
			else
			{
				Toast(slot.Name + " was already empty.");
			}
		}

		private void Jump(Slot slot)
		{
			if (!_config.Locations.TryGetValue(slot.Name, out SavedLocation location) || location == null)
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
			string keys = (_slots.Count == 0) ? "(none configured)" : string.Join(" ", SlotNames());
			string line = "BIND MODE - press " + keys + " to save this view";
			if (_clearModifier != KeyNames.Modifier.None)
			{
				line += "  |  " + _clearModifier + "+key clears it";
			}

			return line;
		}

		private string[] SlotNames()
		{
			string[] names = new string[_slots.Count];
			for (int i = 0; i < _slots.Count; i++)
			{
				names[i] = _slots[i].Name;
			}

			return names;
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
