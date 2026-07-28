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

		/// <summary>Bumped when the file's shape changes, so Load can migrate it.</summary>
		public const int CurrentVersion = 2;

		/// <summary>Slot keys used to be the dictionary keys, before the bindings moved
		/// into Options -> Controls; this is the order they mapped to slots 1-7.</summary>
		private static readonly string[] LegacySlotKeys = { "F5", "F6", "F7", "F8", "F9", "F10", "F12" };

		public int Version;

		/// <summary>Held alongside a slot key, binds without entering bind mode. Off by
		/// default because desktop environments tend to claim Alt/Ctrl + F-key.</summary>
		public string QuickBindModifier = "None";

		/// <summary>Held alongside a slot key in bind mode, clears that slot instead of binding it.</summary>
		public string ClearModifier = "Shift";

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
					HotkeyConfig fresh = new HotkeyConfig { Version = CurrentVersion };
					fresh.Save();
					return fresh;
				}

				HotkeyConfig loaded = JsonConvert.DeserializeObject<HotkeyConfig>(File.ReadAllText(Path));
				if (loaded == null)
				{
					NoonUtility.LogWarning("Location Hotkeys: " + Path + " is empty; using defaults.");
					return new HotkeyConfig { Version = CurrentVersion };
				}

				// A hand-edited file can legitimately omit this; don't make callers null-check.
				if (loaded.Locations == null)
				{
					loaded.Locations = new Dictionary<string, SavedLocation>();
				}

				if (loaded.Version < CurrentVersion)
				{
					loaded.MigrateToSlotIds();
					loaded.Version = CurrentVersion;
					loaded.Save();
				}

				return loaded;
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Location Hotkeys: couldn't read " + Path + " (" + e.Message + "); using defaults. The existing file will not be overwritten until you next bind something.");
				return new HotkeyConfig { Version = CurrentVersion };
			}
		}

		/// <summary>
		/// v1 keyed locations by key name ("F5"); v2 keys them by slot action id
		/// ("lhslot1"), because the player can now rebind the keys in Options.
		/// </summary>
		private void MigrateToSlotIds()
		{
			Dictionary<string, SavedLocation> migrated = new Dictionary<string, SavedLocation>();
			int moved = 0;
			foreach (KeyValuePair<string, SavedLocation> entry in Locations)
			{
				int legacyIndex = Array.IndexOf(LegacySlotKeys, entry.Key);
				if (legacyIndex >= 0)
				{
					migrated[GameBindings.SlotAction(legacyIndex + 1)] = entry.Value;
					moved++;
				}
				else
				{
					migrated[entry.Key] = entry.Value;
				}
			}

			Locations = migrated;
			if (moved > 0)
			{
				NoonUtility.Log("Location Hotkeys: moved " + moved + " saved location(s) onto the new rebindable slots.");
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
