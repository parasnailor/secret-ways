# Book of Hours — Secret Ways

Save locations for quick & easy navigation with a radial menu or hotkeys.

## About

Adds 10 slots for saved locations. Saved locations can be quickly panned to with
the radial menu or with dedicated hotkeys.

## Usage

- **Hold or tap `T`** to open the radial menu; select an empty slot to bind a
  location, or an occupied one to pan there.
- **`Page Up`** enters bind mode; press one of the ten location keys to bind the
  current view.
- **Press `U` `I` `O` `P` `J` `K` `L` `;` `,` `.`** to jump to a saved view.
- **Manage your bound locations** with the radial menu pinned (**tap `T`**) —
  rename (`F2`), delete (`Del`), overwrite (`Ctrl`+click), or refresh the image
  preview (`R`).

## Hotkeys

All hotkeys are rebindable under **Options → Controls**, in a SECRET WAYS section
below the base game's bindings. Locations follow the slot, not the key, so
rebinding keeps whatever you'd saved. If you'd already rebound a base-game action
onto one of the mod's defaults, that slot starts unbound rather than firing
alongside yours.

## Requirements

**Book of Hours will not load any DLL mod unless the GHIRBI gatekeeper mod is
installed and enabled.**
[Subscribe to GHIRBI](https://steamcommunity.com/sharedfiles/filedetails/?id=3682369347),
then enable it and Secret Ways in-game.

## Savegames and settings

Each playthrough keeps its own locations. Configuration lives in
`secret_ways.json` next to your saves (Options → BROWSE FILES opens the folder),
with thumbnails in `secret_ways_previews/`. Both are safe to delete.

```json
{
  "Version": 3,
  "QuickBindModifier": "None",
  "ClearModifier": "Shift",
  "RestoreZoom": true,
  "TravelSeconds": 0.45,
  "BindModeTimeoutSeconds": 6.0,
  "ShowOverlay": true,
  "RadialRadius": 260.0,
  "RadialTapSeconds": 0.25,
  "ShowRadialPreviews": true,
  "PreviewWidth": 256,
  "PreviewHeight": 160,
  "Playthroughs": {}
}
```

- `QuickBindModifier` binds without entering bind mode (hold it and press a slot
  key); `"None"`, `"Shift"`, `"Ctrl"` or `"Alt"`.
- `ClearModifier` plus a slot key clears that slot in bind mode.
- `RadialTapSeconds` is the line between a tap and a hold.
- `Playthroughs` holds one entry per playthrough, keyed by slot (`swslot1`…
  `swslot10`); each location's `Label` is the name shown on the wheel.
- `Version` below 3 is migrated on load: settings carry over, the old shared
  locations are dropped, and the file is backed up to `secret_ways.json.pre-v3.bak`.

## Building

You need a copy of Book of Hours and the .NET SDK.

```sh
tools/sync-refs.sh    # copy the game's assemblies into ref/lib (read-only)
tools/build.sh        # compile and stage dist/secret_ways
tools/install.sh      # copy it into the game's mods folder
```

`sync-refs.sh` and `install.sh` take the directory to work against as an
argument, falling back to `BOH_DIR` / `BOH_DATA_DIR` and then to the usual
locations:

```sh
tools/sync-refs.sh "/path/to/Book of Hours"                # the folder holding bh_Data/
tools/install.sh "/path/to/Weather Factory/Book of Hours"  # the save folder; Options → BROWSE FILES opens it
```

Nothing writes to the game install. `ref/lib/` is assemblies copied out of the
game, `ref/decomp/` is decompiled sources.

Then enable **Secret Ways** and **GHIRBI** under Options → Mods and restart —
DLLs are loaded once during startup.

## Publishing (maintainers)

The game uploads to the Workshop itself, from Options → Mods, using
`mod/synopsis.json` as the listing. To ship an update, bump `version` in
`mod/synopsis.json` **and** `<Version>` in [SecretWays.csproj](src/SecretWays.csproj),
run `tools/build.sh && tools/install.sh`, then press upload on the Secret Ways
row in-game. The first upload also needs the item set Public and GHIRBI
(`3682369347`) added under *Add/Remove Required Items*.

`mod/serapeum_catalogue_number.txt` is the maintainer's Workshop item id and
is what makes an upload update that item rather than create a new one. If you
fork the mod, delete it so your first upload creates your own item.