# Book of Hours — Location Hotkeys

Bind spots in Hush House to keys and jump the camera straight back to them.

Tested against Book of Hours `2026.1.f.3` (Linux, Mono).

## Using it

| Key | What it does |
| --- | --- |
| `Page Up` | Toggle **bind mode** |
| `U` `I` `O` `P` `J` `K` `L` `;` `,` `.` | Jump to that saved view — or, in bind mode, save the current view to it |
| `Shift` + slot key (in bind mode) | Clear that slot |

All eleven keys are rebindable in-game under **Options → Controls**, in a
"LOCATION HOTKEYS" section below the base game's bindings. Rebinding there gets
the game's own conflict handling: bind a key that's already in use and the two
actions swap. Saved locations follow the slot, not the key, so remapping a slot
keeps whatever view you'd saved to it.

Bind mode shows a banner at the bottom of the screen and gives up on its own
after six seconds. Pressing a slot key binds it and leaves bind mode straight
away.

Jumping restores position *and* zoom, gliding over ~0.45s. Slot keys do nothing
outside the playfield, while the debug console is open, or while you're typing in
a text field.

The ten defaults sit under the right hand and steer clear of the base game's own
bindings, which include `1`–`4`, `F1`–`F4`, `F11`, the arrows and the letters
`B` `C` `E` `M` `N` `Q` `S`.

## Settings

Everything lives in `location_hotkeys.json`, next to your saves
(`~/.config/unity3d/Weather Factory/Book of Hours/` on Linux; the game's
Options → BROWSE FILES will take you there). It's written on first launch:

```json
{
  "Version": 2,
  "QuickBindModifier": "None",
  "ClearModifier": "Shift",
  "RestoreZoom": true,
  "TravelSeconds": 0.45,
  "BindModeTimeoutSeconds": 6.0,
  "ShowOverlay": true,
  "Locations": {}
}
```

The keys themselves aren't in here — they live in the game's own keybindings, so
change them under Options → Controls.

- **`QuickBindModifier`** lets you bind without entering bind mode (hold it and
  press a slot key). It ships as `"None"`; `"Shift"`, `"Ctrl"` and `"Alt"` work.
- **`RestoreZoom": false`** jumps to the saved spot but keeps your current zoom.
- **`Locations`** is keyed by slot (`lhslot1`…`lhslot10`), not by key, so
  rebinding doesn't orphan anything. Entries take an optional `"Label"` if you
  want to annotate them; the mod preserves it but doesn't display it.
- **`Version`** drives migration. A pre-1.1 file keyed by key name (`"F5"`) is
  remapped to slot ids automatically on first load, and rewritten.

Locations are shared across save files, since Hush House is the same house in
every playthrough. Edit or delete the file to reset.

## Building

```sh
tools/sync-refs.sh    # copy the game's assemblies into ref/lib (read-only)
tools/build.sh        # compile and stage dist/location_hotkeys
tools/install.sh      # copy it into the game's mods folder
```

Both `sync-refs.sh` and `install.sh` take the directory to work against as an
argument, falling back to `BOH_DIR` / `BOH_DATA_DIR` and then to the usual
locations:

```sh
tools/sync-refs.sh "/path/to/Book of Hours"                       # the folder holding bh_Data/
tools/install.sh "/path/to/Weather Factory/Book of Hours"         # the folder Options → BROWSE FILES opens
```

Nothing writes to the game install.

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

The Options → Controls rows are content, not code. `AureateOptionsPanel` builds a
row for every `Setting` entity in the compendium, and picks the keybind prefab for
any whose `datatype` is `String` — so `content/settings/hotkeys.json` is enough to
make them appear, and `content/cultures/hotkeys_loc.json` adds the labels via the
`uilabels$add` merge operation rather than replacing the culture.

What the DLL adds is the other half of the pair. The game matches a `Setting` to an
`InputAction` **by name** (`ControlsController.ApplyExistingKeybindOverrides`,
`KeybindSettingControlStrategy.Rebind`), so [GameBindings.cs](src/GameBindings.cs)
injects actions named `lhbindmode` and `lhslot1`…`lhslot10` into the live
`InputActionAsset`. Get the name wrong and the row silently rebinds `kbfallback`
instead. Because those actions are registered after `ControlsController.Start` has
already replayed saved overrides, `GameBindings` replays its own from each
`Setting.CurrentValue`.

`ref/` is gitignored: `ref/lib/` is assemblies copied out of the game install by
`tools/sync-refs.sh`, `ref/decomp/` is decompiled sources kept for reference.
