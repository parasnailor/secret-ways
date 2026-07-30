using BookOfHoursSecretWays;
using UnityEngine;

/// <summary>
/// Mod entry point.
///
/// Book of Hours' mod loader (ModManager.LoadModDLLs -> Mod.TryInitialiseAssembly)
/// looks up a type by the mod's name with all non-alphanumerics stripped, in the
/// *global* namespace, then invokes its public static Initialise method. So the
/// mod is named "Secret Ways" in synopsis.json, the assembly is SecretWays.dll,
/// and this class must stay here, named exactly this.
/// </summary>
public static class SecretWays
{
	private static GameObject _host;

	public static void Initialise()
	{
		if (_host != null)
		{
			NoonUtility.LogWarning("Secret Ways: Initialise called twice; ignoring the second call.");
			return;
		}

		// DLLs are loaded before the compendium and before any playfield exists, so
		// all we do here is park a permanent listener that waits for the game proper.
		_host = new GameObject("SecretWays");
		Object.DontDestroyOnLoad(_host);
		_host.hideFlags = HideFlags.HideAndDontSave;
		_host.AddComponent<SecretWaysBehaviour>();

		NoonUtility.Log("Secret Ways: initialised.");
	}
}
