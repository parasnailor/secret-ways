using UnityEngine.InputSystem;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>
	/// The slot and bind-mode keys live in the game's own keybindings now; this is
	/// only for the two plain modifiers the mod still reads out of the config file.
	/// </summary>
	public static class KeyNames
	{
		public enum Modifier
		{
			None,
			Shift,
			Ctrl,
			Alt
		}

		public static Modifier ParseModifier(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return Modifier.None;
			}

			switch (name.Trim().ToLowerInvariant())
			{
				case "shift":
					return Modifier.Shift;
				case "ctrl":
				case "control":
					return Modifier.Ctrl;
				case "alt":
					return Modifier.Alt;
				default:
					return Modifier.None;
			}
		}

		public static bool IsHeld(Keyboard keyboard, Modifier modifier)
		{
			switch (modifier)
			{
				case Modifier.Shift:
					return keyboard.shiftKey.isPressed;
				case Modifier.Ctrl:
					return keyboard.ctrlKey.isPressed;
				case Modifier.Alt:
					return keyboard.altKey.isPressed;
				default:
					return false;
			}
		}
	}
}
