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

		public const string RadialAction = "lhradial";

		public const int SlotCount = 10;

		/// <summary>Slot n's action name. Locations are keyed by these, not by key
		/// name, so a rebind in the options menu doesn't orphan a saved location.</summary>
		public static string SlotAction(int slot)
		{
			return "lhslot" + slot;
		}

		/// <summary>
		/// Defaults only - once the player rebinds, the Setting entity wins.
		/// Index-aligned with ActionNames().
		///
		/// None of these collide with the base game's own bindings, but nothing stops
		/// them colliding with a player's *rebound* ones: the game only looks for
		/// duplicates when rebinding through its options menu (KeybindSettingControlStrategy
		/// .FindDuplicateBinding), never when actions are added. A collision means both
		/// actions fire until the player rebinds one of them.
		/// </summary>
		private static readonly string[] DefaultPaths =
		{
			"<Keyboard>/pageUp",
			"<Keyboard>/t",
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

		/// <summary>Actions whose default key was already taken, so they start unset.</summary>
		private readonly List<string> _yielded = new List<string>();

		public bool Ready { get; private set; }

		/// <summary>Action ids left unbound because their default was already in use.</summary>
		public IList<string> Yielded => _yielded;

		private static IEnumerable<string> ActionNames()
		{
			yield return BindModeAction;
			yield return RadialAction;
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

					// The binding slot has to exist either way: the options row reads
					// action.bindings[i] to show and rebind the key, and would throw on
					// an action with none. So always add it, then blank the path if
					// something already has that key.
					string path = DefaultPaths[index];
					action.AddBinding(path, groups: BindingGroups);

					if (IsPathAlreadyBound(asset, action, path))
					{
						action.ApplyBindingOverride(string.Empty);
						_yielded.Add(name);
					}
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

			if (_yielded.Count > 0)
			{
				NoonUtility.LogWarning("Location Hotkeys: left " + _yielded.Count
					+ " key(s) unset because you'd already bound their defaults to something else - "
					+ string.Join(", ", _yielded.ToArray())
					+ ". Set them under Options > Controls.");
			}

			return true;
		}

		/// <summary>
		/// Whether anything else already answers to this key. Mirrors the game's own
		/// KeybindSettingControlStrategy.FindDuplicateBinding, which it only runs when
		/// rebinding through the options menu - never when actions are added, which is
		/// why we have to check for ourselves.
		/// </summary>
		private static bool IsPathAlreadyBound(InputActionAsset asset, InputAction ours, string path)
		{
			foreach (InputActionMap actionMap in asset.actionMaps)
			{
				foreach (InputAction action in actionMap.actions)
				{
					if (action == ours)
					{
						continue;
					}

					foreach (InputBinding binding in action.bindings)
					{
						if (!binding.isComposite && binding.effectivePath == path)
						{
							return true;
						}
					}
				}
			}

			return false;
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

					// The player has since given this one a key of its own, so whatever
					// its default clashed with no longer matters.
					_yielded.Remove(entry.Key);
				}
			}
		}

		public bool WasPressedThisFrame(string actionName)
		{
			return _actions.TryGetValue(actionName, out InputAction action) && action.enabled && action.triggered;
		}

		/// <summary>For the radial's hold-to-open: true for as long as the key is down.</summary>
		public bool IsHeld(string actionName)
		{
			return _actions.TryGetValue(actionName, out InputAction action) && action.enabled && action.IsPressed();
		}

		public bool WasReleasedThisFrame(string actionName)
		{
			return _actions.TryGetValue(actionName, out InputAction action) && action.enabled && action.WasReleasedThisFrame();
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
