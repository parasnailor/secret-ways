# Book of Hours — Location Hotkeys

Bind spots in Hush House to keys and jump the camera straight back to them.

Tested against Book of Hours `2026.1.f.3` (Linux, Mono).

## Using it

| Key | What it does |
| --- | --- |
| `Page Up` | Toggle **bind mode** |
| `F5` `F6` `F7` `F8` `F9` `F10` `F12` | Jump to that saved view — or, in bind mode, save the current view to it |
| `Shift` + slot key (in bind mode) | Clear that slot |

Bind mode shows a banner at the bottom of the screen and gives up on its own after
six seconds. Pressing a slot key binds it and leaves bind mode straight away.

Jumping restores position *and* zoom, gliding over ~0.45s. Slot keys do nothing
outside the playfield, while the debug console is open, or while you're typing in
a text field.

The default slot keys were picked because the base game leaves them alone — it
already uses `1`–`4` for zoom presets, `F1`–`F4` for trays and `F11` for the HUD.

## Settings

Everything lives in `location_hotkeys.json`, next to your saves
(`~/.config/unity3d/Weather Factory/Book of Hours/` on Linux; the game's
Options → BROWSE FILES will take you there). It's written on first launch:

```json
{
  "BindModeKey": "PageUp",
  "QuickBindModifier": "None",
  "ClearModifier": "Shift",
  "SlotKeys": ["F5", "F6", "F7", "F8", "F9", "F10", "F12"],
  "RestoreZoom": true,
  "TravelSeconds": 0.45,
  "BindModeTimeoutSeconds": 6.0,
  "ShowOverlay": true,
  "Locations": {}
}
```

- **Key names** are [`UnityEngine.InputSystem.Key`](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/api/UnityEngine.InputSystem.Key.html)
  values — `PageUp`, `Home`, `Backslash`, `Semicolon`, `Digit5`, `F5`, and so on.
  Avoid `Backquote` and `Quote`: the game uses them for its debug console.
- **`QuickBindModifier`** lets you bind without entering bind mode (hold it and
  press a slot key). It ships as `"None"` because Linux desktops tend to claim
  `Alt`/`Ctrl` + F-key for window management. `"Shift"`, `"Ctrl"` and `"Alt"` work.
- **`RestoreZoom": false`** jumps to the saved spot but keeps your current zoom.
- **`Locations`** entries take an optional `"Label"` if you want to annotate them;
  the mod preserves it but doesn't display it.

Locations are shared across save files, since Hush House is the same house in
every playthrough. Edit or delete the file to reset.

## Building

```sh
tools/sync-refs.sh    # copy the game's assemblies into ref/lib (read-only)
tools/build.sh        # compile and stage dist/location_hotkeys
tools/install.sh      # copy it into the game's mods folder
```

`sync-refs.sh` and `install.sh` take `BOH_DIR` / `BOH_DATA_DIR` if your install
or save folder isn't in the usual place. Nothing writes to the game install.

### Enabling it

Book of Hours will not load *any* DLL mod unless a gatekeeper mod named **GHIRBI**
is installed and enabled — that's the game's own consent gate for running
third-party code (`ModManager.Safety.IsDLLAllowed`). Get it from the Steam
Workshop and enable it plus "Location Hotkeys" under Options → Mods.

If you'd rather not go through Steam, `tools/install.sh --enable-dll-mods` writes
a local GHIRBI folder and switches both mods on. Same consequence either way: with
GHIRBI enabled, every enabled DLL mod runs arbitrary code in the game process.

Restart the game after changing any of this — DLLs are loaded once during startup.

## How it works

`ModManager` loads `dll/LocationHotkeys.dll` and calls the static `Initialise()` on
the global-namespace `LocationHotkeys` class (the name has to match the mod's name
from `synopsis.json` with non-alphanumerics stripped). That happens before the
compendium loads and long before any playfield exists, so `Initialise` just parks
a `DontDestroyOnLoad` MonoBehaviour that waits for the game to catch up.

From there it's all public game API — no patching:

- `Watchman.Get<CamOperator>()` — the tabletop camera. `GetAttachedCamera()
  .transform.position` is the viewport (`z` is zoom height, negative), and
  `PointAtTableLevelAtHeight` flies to a saved one with the game's own easing.
- `Watchman.Get<StageHand>().SceneIsActive(dictum.PlayfieldScene)` — gates input
  to the playfield, the same check `OptionsPanel` uses.
- `CamOperator.StopAllMovement()` before a jump, so held pan keys and drag drift
  don't fight the glide.

`ref/` holds assemblies copied out of the game install plus decompiled sources for
reference. It's gitignored; regenerate it with `tools/sync-refs.sh`.
