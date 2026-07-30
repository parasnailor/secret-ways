using System.Globalization;
using SecretHistories.Entities;
using SecretHistories.UI;

namespace BookOfHoursSecretWays
{
	/// <summary>
	/// Identifies the playthrough the player is currently in, so saved locations can
	/// be scoped to it.
	///
	/// The game has no runtime notion of a "current save file": a named save is
	/// depersisted into the live state and the provider thrown away, after which
	/// autosaves go to AUTOSAVE.json regardless. The protagonist is the thing that
	/// actually identifies a run, and Character.DateTimeCreated is written into the
	/// save and restored on load (CharacterCreationCommand.ExecuteToProtagonist ->
	/// SetCreatedAtTime), so it survives both reloading and Save As.
	/// </summary>
	public static class Playthrough
	{
		/// <summary>
		/// The current playthrough's id, plus a human-readable label for anyone
		/// hand-editing the config. False outside a game, where there's no protagonist.
		/// </summary>
		public static bool TryGetCurrent(out string id, out string label)
		{
			id = null;
			label = null;

			Stable stable = Watchman.Get<Stable>();
			Character character = (stable == null) ? null : stable.Protag();
			if (character == null)
			{
				return false;
			}

			// Ticks in UTC, so that changing timezone doesn't shift the key: the save
			// stores an offset, which Newtonsoft parses back into local time.
			System.DateTime created = character.DateTimeCreated;
			if (created == default(System.DateTime))
			{
				return false;
			}

			id = created.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
			label = DescribeCharacter(character);
			return true;
		}

		/// <summary>Cosmetic only. Name is worth having even though it starts out as the
		/// "[click to name]" placeholder, and Profession carries the legacy line that
		/// actually tells two runs apart.</summary>
		private static string DescribeCharacter(Character character)
		{
			string name = (character.Name == null) ? string.Empty : character.Name.Trim();
			string profession = (character.Profession == null) ? string.Empty : character.Profession.Trim();

			if (name.Length == 0)
			{
				return profession;
			}

			return (profession.Length == 0) ? name : name + " - " + profession;
		}
	}
}
