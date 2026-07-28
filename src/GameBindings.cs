using System.Collections.Generic;
using SecretHistories.Entities;
using SecretHistories.Infrastructure;
using SecretHistories.UI;
using UnityEngine.InputSystem;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>
	/// Registers the mod's keys as real InputActions inside the game's own action
	/// asset, so they show up in Options -> Controls as rebindable rows.
	///
	/// The game ties the two together by name: AureateOptionsPanel builds a row for
	/// every Setting entity (ours come from content/settings/hotkeys.json), and
	/// KeybindSettingControlStrategy rebinds whatever action shares the setting's id.
	/// Miss the name and the row silently rebinds "kbfallback" instead.
	/// </summary>
	public class GameBindings
	{
		public const string BindModeAction = "lhbindmode";

		public const int SlotCount = 10;

		/// <summary>Slot n's action name. Locations are keyed by these, not by key
		/// name, so a rebind in the options menu doesn't orphan a saved location.</summary>
		public static string SlotAction(int slot)
		{
			return "lhslot" + slot;
		}

		/// <summary>Defaults only - once the player rebinds, the Setting entity wins.
		/// Chosen to avoid the base game's own bindings.</summary>
		private static readonly string[] DefaultPaths =
		{
			"<Keyboard>/pageUp",
			"<Keyboard>/u",
			"<Keyboard>/i",
			"<Keyboard>/o",
			"<Keyboard>/p",
			"<Keyboard>/j",
			"<Keyboard>/k",
			"<Keyboard>/l",
			"<Keyboard>/semicolon",
			"<Keyboard>/comma",
			"<Keyboard>/period"
		};

		/// <summary>Both schemes, so the rows stay visible whichever one is active;
		/// see ControlsController.IsActionRelevantForCurrentControlScheme.</summary>
		private const string BindingGroups = "PC;SteamDeck";

		private readonly Dictionary<string, InputAction> _actions = new Dictionary<string, InputAction>();

		public bool Ready { get; private set; }

		private static IEnumerable<string> ActionNames()
		{
			yield return BindModeAction;
			for (int slot = 1; slot <= SlotCount; slot++)
			{
				yield return SlotAction(slot);
			}
		}

		/// <summary>
		/// Attaches to the live action asset. Returns false until ControlsController
		/// exists, which it won't until a scene with player input is up.
		/// </summary>
		public bool TryAttach()
		{
			if (Ready)
			{
				return true;
			}

			ControlsController controls = Watchman.Get<ControlsController>();
			PlayerInput playerInput = (controls == null) ? null : controls.GetPlayerInput();
			InputActionAsset asset = (playerInput == null) ? null : playerInput.actions;
			if (asset == null || asset.actionMaps.Count == 0)
			{
				return false;
			}

			InputActionMap map = asset.actionMaps[0];

			// Actions can only be added to a disabled map.
			bool wasEnabled = map.enabled;
			if (wasEnabled)
			{
				map.Disable();
			}

			int index = 0;
			foreach (string name in ActionNames())
			{
				InputAction action = asset.FindAction(name);
				if (action == null)
				{
					action = map.AddAction(name, InputActionType.Button);
					action.AddBinding(DefaultPaths[index], groups: BindingGroups);
				}

				_actions[name] = action;
				index++;
			}

			if (wasEnabled)
			{
				map.Enable();
			}

			ApplySavedOverrides();
			Ready = true;
			NoonUtility.Log("Location Hotkeys: registered " + _actions.Count + " actions in the game's keybindings.");
			return true;
		}

		/// <summary>
		/// ControlsController.ApplyExistingKeybindOverrides has already run by the time
		/// our actions exist, so we replay the player's saved rebinds ourselves.
		/// </summary>
		private void ApplySavedOverrides()
		{
			Compendium compendium = Watchman.Get<Compendium>();
			if (compendium == null)
			{
				return;
			}

			foreach (KeyValuePair<string, InputAction> entry in _actions)
			{
				Setting setting = compendium.GetEntityById<Setting>(entry.Key);
				string saved = (setting == null || setting.CurrentValue == null) ? null : setting.CurrentValue.ToString();
				if (!string.IsNullOrEmpty(saved))
				{
					entry.Value.ApplyBindingOverride(saved);
				}
			}
		}

		public bool WasPressedThisFrame(string actionName)
		{
			return _actions.TryGetValue(actionName, out InputAction action) && action.enabled && action.triggered;
		}

		/// <summary>The key currently bound to an action, for the on-screen prompt.</summary>
		public string DisplayString(string actionName)
		{
			if (!_actions.TryGetValue(actionName, out InputAction action))
			{
				return "?";
			}

			string display = action.GetBindingDisplayString();
			return string.IsNullOrEmpty(display) ? "?" : display;
		}
	}
}
