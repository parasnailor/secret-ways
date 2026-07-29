# Book of Hours — Location Hotkeys

Bind spots in Hush House to keys and jump the camera straight back to them.

Tested against Book of Hours `2026.1.f.3` (Linux, Mono).

## Using it

| Key | What it does |
| --- | --- |
| `Page Down` | Open the **location wheel** |
| `Page Up` | Toggle **bind mode** |
| `U` `I` `O` `P` `J` `K` `L` `;` `,` `.` | Jump to that saved view — or, in bind mode, save the current view to it |
| `Shift` + slot key (in bind mode) | Clear that slot |

All twelve keys are rebindable in-game under **Options → Controls**, in a
"LOCATION HOTKEYS" section below the base game's bindings. Rebinding there gets
the game's own conflict handling: bind a key that's already in use and the two
actions swap. Saved locations follow the slot, not the key, so remapping a slot
keeps whatever view you'd saved to it.

The ten slot defaults sit under the right hand and steer clear of the base
game's own bindings, which include `1`–`4`, `F1`–`F4`, `F11`, the arrows and the
letters `B` `C` `E` `M` `N` `Q` `S`.

### The wheel

**Hold `Page Down`**, flick the mouse towards a place and let go to fly there —
you don't have to land on it, only point roughly its way. Releasing near the
middle means "never mind".

**Tap `Page Down`** instead and the wheel stays up to be clicked, which is how
you edit it. With the wheel up, whatever you're pointing at can be:

| Input | What it does |
| --- | --- |
| Click | Go there — or, on an empty place, save the current view to it |
| `Ctrl` + click | Overwrite it with the current view |
| `F2` | Rename it — `Enter` saves, `Escape` cancels |
| `Del` | Clear it |
| `R` | Take a new photo: flies there, then re-captures the thumbnail |

Everything but plain click needs a place that's already saved; an empty one can
only be filled. Renaming is a real text field, so selection, click-and-drag,
`Ctrl`+arrows, `Ctrl`+`Backspace` and the clipboard all work, and the old name
starts selected so typing replaces it.

Right-click, `Esc`, clicking the middle, or pressing `Page Down` again all close
it. While the wheel is up the game is held still — no panning, zooming, hotkeys
or card dragging — and everything goes back to normal when it closes.

Each place shows a photo of the view saved to it, taken at the moment you bound
it. Places bound before this version have no photo and show their slot number
instead; point at one and press `R` to fill it in. Names come from the same
`Label` you can set by hand in the config.

### Bind mode

Bind mode shows a banner at the bottom of the screen and gives up on its own
after six seconds. Pressing a slot key binds it and leaves bind mode straight
away.

Jumping restores position *and* zoom, gliding over ~0.45s. Slot keys do nothing
outside the playfield, while the debug console is open, or while you're typing in
a text field.

## Settings

Everything lives in `location_hotkeys.json`, next to your saves
(`~/.config/unity3d/Weather Factory/Book of Hours/` on Linux; the game's
Options → BROWSE FILES will take you there). It's written on first launch:

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

The keys themselves aren't in here — they live in the game's own keybindings, so
change them under Options → Controls.

- **`QuickBindModifier`** lets you bind without entering bind mode (hold it and
  press a slot key). It ships as `"None"`; `"Shift"`, `"Ctrl"` and `"Alt"` work.
- **`RestoreZoom": false`** jumps to the saved spot but keeps your current zoom.
- **`RadialTapSeconds`** is the line between a tap and a hold. Hold the wheel key
  longer than this and letting go picks whatever you're pointing at; let go
  sooner and the wheel stays up to be clicked.
- **`RadialRadius`** is how far the wheel's places sit from its centre, and
  **`ShowRadialPreviews": false`** drops the photos for plain plates.
- **`Playthroughs`** holds one entry per playthrough, each with its own
  `Locations` keyed by slot (`lhslot1`…`lhslot10`) rather than by key, so
  rebinding doesn't orphan anything. Each location's `"Label"` is the name shown
  on the wheel, and editing it here is the same as renaming it with `F2`.
- **`Version`** drives migration. Upgrading from a pre-1.3 file drops the
  locations it held — they were shared by every playthrough, and there's no
  sound way to say which one they belonged to — but the file is copied to
  `location_hotkeys.json.pre-v3.bak` first, and your settings carry over.

Each playthrough gets its own locations, so a new game starts with nothing bound.
A playthrough is identified by its protagonist rather than by a save file, which
means locations survive reloading and Save As. Entries for abandoned playthroughs
just sit there; delete them by hand, or delete the file to reset everything.

The wheel's photos sit beside the config in `location_hotkeys_previews/`, one
folder per playthrough and one PNG per slot. They're safe to delete — the wheel
falls back to slot numbers, and `R` takes them again.

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
- `Watchman.Get<Stable>().Protag()` — which playthrough we're in. There's no
  "current save file" to key off: a named save is depersisted into the live state
  and its provider discarded, after which autosaves go to `AUTOSAVE.json` anyway.
  The protagonist's `DateTimeCreated` is persisted and restored with the save, so
  [Playthrough.cs](src/Playthrough.cs) uses its UTC ticks as the id.
- `Watchman.Get<LocalNexus>().DisablePlayerInput(0f)` — holds the game still while
  the wheel is up. Zero seconds means "until we say so"; it also locks the camera
  and switches off every base-game hotkey. It does *not* stop cards being clicked,
  though — `Token` never consults the flag — so the wheel's full-screen scrim is
  what actually keeps the tabletop out of it.

### The wheel's UI

It's built from bare `GameObject`s on the game's own overlay canvas, so it
inherits the player's UI-scale setting and matches the game's look:

- The overlay transform is private on `Meniscate`, but `DisplayInOverlayAtScreenCentre`
  reparents whatever it's handed — so [NativeUi.cs](src/NativeUi.cs) hands it a
  throwaway object and reads back the parent, rather than reflecting.
- Fonts come from `ILocStringProvider.GetFont(style, culture.FontScript)`, the
  same call the game's own windows make, so the wheel follows the language
  setting. Colours are `UIStyle`'s, and clicks and hovers use `SoundManager`.
- Panel and ring art is generated at load from a signed-distance function, which
  gives matching fills and outlines from one shape. The game ships no reusable
  panel prefab, and sprites loaded through `ModManager` come back with a zero
  border and so can't 9-slice, which rules out shipping PNGs for the frames.
  Panel colours *are* the game's, but they're serialised onto prefabs rather than
  written in code — there are only two `Color32` literals in the whole of
  `SecretHistories.Main` — so there's nothing to read at runtime and the values in
  [NativeUi.cs](src/NativeUi.cs) are matched by eye.
- The places sit on an **ellipse**, not a circle. They're half again as wide as
  they are tall, so on a circle the ones nearest twelve and six o'clock — where
  the arc runs horizontally — crowd together and clip, while the ones at three
  and nine have room to spare.
- Renaming hands the keyboard to a real `TMP_InputField`, built and wired in
  code, rather than a hand-rolled character buffer — that's where selection, word
  jumps and the clipboard come from. It also means the game's own
  `UIController.IsEditingText()` sees it and suppresses base-game hotkeys and the
  abort key for free while you're typing.
- Hover is resolved by direction from the wheel's centre rather than by pointer
  events on each place. That's what lets you flick towards one and release
  without landing on it, and it means holding and clicking share a code path. The
  hit test divides out the horizontal stretch first, which maps the ellipse back
  onto a circle so the angle lands exactly on the place it looks like it should.
  The game's own `ExtendedRadialLayoutGroup` is unused here: its `distance` is a
  private serialised field with no default, so it stacks everything at the centre
  unless reflected, and being a `LayoutGroup` it won't let go of its children.

Thumbnails are captured at bind time, through a throwaway `Camera` that
`CopyFrom`s the real one and renders into a `RenderTexture`. Bind time is the
only honest moment: the zoom-dependent scene state — wall translucency, darkening
— is driven off camera `z` by `ZoomEffectController`, so a later render from
somewhere else would show the wrong thing. Rendering off-screen also keeps the
HUD out of the shot for free, since screen-space-overlay canvases aren't drawn by
any camera.

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
