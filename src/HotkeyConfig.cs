using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BookOfHoursLocationHotkeys
{
	/// <summary>A camera position saved against a slot key.</summary>
	public class SavedLocation
	{
		public float X;

		public float Y;

		/// <summary>Camera z, which in this game is the zoom height (negative).</summary>
		public float Z;

		/// <summary>Free-text note; the mod never writes this, but it survives round-trips.</summary>
		public string Label;

		[JsonIgnore]
		public Vector2 TablePosition => new Vector2(X, Y);
	}

	/// <summary>
	/// User-editable settings plus the saved locations themselves, persisted next to
	/// the save files so a mod reinstall doesn't lose them.
	/// </summary>
	public class HotkeyConfig
	{
		/// <summary>Shortest camera travel the game will actually perform: with a
		/// duration of zero, CamOperator's lerp loop never runs a single iteration.</summary>
		private const float MinimumTravelSeconds = 0.05f;

		private const string FileName = "location_hotkeys.json";

		/// <summary>Key that toggles bind mode.</summary>
		public string BindModeKey = "PageUp";

		/// <summary>Held alongside a slot key, binds without entering bind mode. Off by
		/// default because desktop environments tend to claim Alt/Ctrl + F-key.</summary>
		public string QuickBindModifier = "None";

		/// <summary>Held alongside a slot key in bind mode, clears that slot instead of binding it.</summary>
		public string ClearModifier = "Shift";

		/// <summary>The slot keys, in the order the overlay lists them.</summary>
		/// <remarks>Replace, not Auto: otherwise Newtonsoft appends the file's entries
		/// to the defaults above and the list doubles on every load.</remarks>
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> SlotKeys = new List<string> { "F5", "F6", "F7", "F8", "F9", "F10", "F12" };

		/// <summary>Restore the saved zoom height as well as the position.</summary>
		public bool RestoreZoom = true;

		/// <summary>Seconds the camera takes to glide to a saved location.</summary>
		public float TravelSeconds = 0.45f;

		/// <summary>Bind mode gives up on its own after this long.</summary>
		public float BindModeTimeoutSeconds = 6f;

		/// <summary>Draw the bind-mode banner and the confirmation toasts.</summary>
		public bool ShowOverlay = true;

		/// <summary>Slot key name -> saved location.</summary>
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, SavedLocation> Locations = new Dictionary<string, SavedLocation>();

		[JsonIgnore]
		public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

		[JsonIgnore]
		public float ClampedTravelSeconds => Mathf.Max(TravelSeconds, MinimumTravelSeconds);

		public static HotkeyConfig Load()
		{
			try
			{
				if (!File.Exists(Path))
				{
					HotkeyConfig fresh = new HotkeyConfig();
					fresh.Save();
					return fresh;
				}

				HotkeyConfig loaded = JsonConvert.DeserializeObject<HotkeyConfig>(File.ReadAllText(Path));
				if (loaded == null)
				{
					NoonUtility.LogWarning("Location Hotkeys: " + Path + " is empty; using defaults.");
					return new HotkeyConfig();
				}

				// A hand-edited file can legitimately omit these; don't make callers null-check.
				if (loaded.SlotKeys == null)
				{
					loaded.SlotKeys = new List<string>();
				}

				if (loaded.Locations == null)
				{
					loaded.Locations = new Dictionary<string, SavedLocation>();
				}

				return loaded;
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Location Hotkeys: couldn't read " + Path + " (" + e.Message + "); using defaults. The existing file will not be overwritten until you next bind something.");
				return new HotkeyConfig();
			}
		}

		public void Save()
		{
			try
			{
				// Write-then-move so a crash mid-write can't leave a truncated config.
				string temp = Path + ".tmp";
				File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented));
				if (File.Exists(Path))
				{
					File.Delete(Path);
				}

				File.Move(temp, Path);
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Location Hotkeys: couldn't write " + Path + ": " + e.Message);
			}
		}
	}
}
