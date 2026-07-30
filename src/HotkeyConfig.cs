using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BookOfHoursSecretWays
{
	/// <summary>A camera position saved against a slot key.</summary>
	public class SavedLocation
	{
		public float X;

		public float Y;

		/// <summary>Camera z, which in this game is the zoom height (negative).</summary>
		public float Z;

		/// <summary>What the player calls this place. Shown on the wheel and editable
		/// there; empty falls back to the slot number.</summary>
		public string Label;

		[JsonIgnore]
		public Vector2 TablePosition => new Vector2(X, Y);

		/// <summary>What to call this place on screen, for a slot that has one saved.</summary>
		public string DisplayName(int slot)
		{
			return string.IsNullOrEmpty(Label) ? "Location " + slot : Label;
		}
	}

	/// <summary>One playthrough's saved locations, keyed by slot action id.</summary>
	public class PlaythroughLocations
	{
		/// <summary>Who this run belongs to. Cosmetic - refreshed on every write, since
		/// the player can name their character partway through.</summary>
		public string Label;

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, SavedLocation> Locations = new Dictionary<string, SavedLocation>();
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

		private const string FileName = "secret_ways.json";

		/// <summary>Bumped when the file's shape changes, so Load can migrate it.</summary>
		public const int CurrentVersion = 3;

		public int Version;

		/// <summary>Held alongside a slot key, binds without entering bind mode. Off by
		/// default so that a slot key on its own always means "go there".</summary>
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

		/// <summary>How far the wheel's slots sit from its centre, in reference pixels.</summary>
		public float RadialRadius = 260f;

		/// <summary>Hold the wheel key longer than this and releasing it picks whatever
		/// the mouse points at; let go sooner and the wheel stays up to be clicked.</summary>
		public float RadialTapSeconds = 0.25f;

		/// <summary>Show thumbnails on the wheel. Off draws slots as plain plates.</summary>
		public bool ShowRadialPreviews = true;

		/// <summary>Size of the captured thumbnails, in pixels.</summary>
		public int PreviewWidth = 256;

		public int PreviewHeight = 160;

		/// <summary>Playthrough id (see Playthrough.TryGetCurrent) -> that run's locations.
		/// Scoped per run rather than shared, so a new game starts with nothing bound.</summary>
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, PlaythroughLocations> Playthroughs = new Dictionary<string, PlaythroughLocations>();

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
					NoonUtility.LogWarning("Secret Ways: " + Path + " is empty; using defaults.");
					return new HotkeyConfig { Version = CurrentVersion };
				}

				// A hand-edited file can legitimately omit this; don't make callers null-check.
				if (loaded.Playthroughs == null)
				{
					loaded.Playthroughs = new Dictionary<string, PlaythroughLocations>();
				}

				if (loaded.Version < CurrentVersion)
				{
					loaded.MigrateToPerPlaythrough();
					loaded.Version = CurrentVersion;
					loaded.Save();
				}

				return loaded;
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Secret Ways: couldn't read " + Path + " (" + e.Message + "); using defaults. The existing file will not be overwritten until you next bind something.");
				return new HotkeyConfig { Version = CurrentVersion };
			}
		}

		/// <summary>
		/// Up to v2 every playthrough shared one flat "Locations" map; v3 scopes them per
		/// playthrough. There's no sound way to decide which run the shared ones belonged
		/// to, so they're dropped - Deserialize has already discarded the field by the time
		/// we get here, since it no longer exists on this class. Settings are kept.
		/// </summary>
		private void MigrateToPerPlaythrough()
		{
			// Dropping locations is irreversible, so leave the old file behind.
			try
			{
				string backup = Path + ".pre-v3.bak";
				File.Copy(Path, backup, true);
				NoonUtility.Log("Secret Ways: saved locations are now per-playthrough, so the old shared ones were cleared. The previous file is at " + backup + ".");
			}
			catch (Exception e)
			{
				NoonUtility.LogWarning("Secret Ways: saved locations are now per-playthrough, so the old shared ones were cleared, but the backup couldn't be written (" + e.Message + ").");
			}
		}

		public bool TryGetLocation(string playthroughId, string slotAction, out SavedLocation location)
		{
			location = null;
			return Playthroughs.TryGetValue(playthroughId, out PlaythroughLocations run)
				&& run != null
				&& run.Locations != null
				&& run.Locations.TryGetValue(slotAction, out location)
				&& location != null;
		}

		public void SetLocation(string playthroughId, string label, string slotAction, SavedLocation location)
		{
			if (!Playthroughs.TryGetValue(playthroughId, out PlaythroughLocations run) || run == null)
			{
				run = new PlaythroughLocations();
				Playthroughs[playthroughId] = run;
			}

			// A hand-edited file can drop this, and the name can change mid-run.
			if (run.Locations == null)
			{
				run.Locations = new Dictionary<string, SavedLocation>();
			}

			run.Label = label;
			run.Locations[slotAction] = location;
			Save();
		}

		/// <summary>True if there was anything there to clear.</summary>
		public bool ClearLocation(string playthroughId, string slotAction)
		{
			if (!Playthroughs.TryGetValue(playthroughId, out PlaythroughLocations run)
				|| run == null
				|| run.Locations == null
				|| !run.Locations.Remove(slotAction))
			{
				return false;
			}

			Save();
			return true;
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
				NoonUtility.LogWarning("Secret Ways: couldn't write " + Path + ": " + e.Message);
			}
		}
	}
}
