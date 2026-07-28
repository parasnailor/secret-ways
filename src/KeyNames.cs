using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>Turns the strings in the config file into Input System keys.</summary>
	public static class KeyNames
	{
		/// <summary>A modifier read from config: either a real pair of keys, or nothing.</summary>
		public enum Modifier
		{
			None,
			Shift,
			Ctrl,
			Alt
		}

		public static bool TryParseKey(string name, out Key key)
		{
			key = Key.None;
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}

			return Enum.TryParse<Key>(name.Trim(), true, out key) && key != Key.None;
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

		public static bool WasPressedThisFrame(Keyboard keyboard, Key key)
		{
			if (key == Key.None)
			{
				return false;
			}

			KeyControl control = keyboard[key];
			return control != null && control.wasPressedThisFrame;
		}
	}
}
