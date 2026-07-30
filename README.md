# Book of Hours — Secret Ways

Bind spots in Hush House to keys and jump the camera straight back to them.

Tested against Book of Hours `2026.1.f.3` (Linux, Mono).

## Using it

| Key | What it does |
| --- | --- |
| `T` | Open the **location wheel** |
| `Page Up` | Toggle **bind mode** |
| `U` `I` `O` `P` `J` `K` `L` `;` `,` `.` | Jump to that saved view — or, in bind mode, save the current view to it |
| `Shift` + slot key (in bind mode) | Clear that slot |

All twelve keys are rebindable in-game under **Options → Controls**, in a
"SECRET WAYS" section below the base game's bindings. Rebinding there gets
the game's own conflict handling: bind a key that's already in use and the two
actions swap. Saved locations follow the slot, not the key, so remapping a slot
keeps whatever view you'd saved to it.

Every default steers clear of the base game's own bindings, which are `1`–`4`,
`F1`–`F4`, `F11`, the arrows, `Backspace` `Ctrl` `Esc` `Shift` `Space` `Tab`,
`+`/`-` on the numpad, and the letters `B` `C` `E` `M` `N` `Q` `S`. The ten slot
defaults also sit together under the right hand.

**If you've rebound a base-game action onto one of these keys yourself**, the mod
gives way: that slot starts with no key at all rather than quietly firing
alongside yours, and you're told once on screen and in the log. Set it to
whatever you like under Options → Controls and it behaves normally from then on.

The mod has to check for itself, because the game only looks for duplicate keys
when *you* rebind through its menu — never when a mod adds bindings at startup.

### The wheel

**Hold `T`**, flick the mouse towards a place and let go to fly there — you don't
have to land on it, only point roughly its way. Releasing near the middle means
"never mind".

**Tap `T`** instead and the wheel stays up to be clicked, which is how you edit
it. With the wheel up, whatever you're pointing at can be:

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

Right-click, `Esc`, clicking the middle, or pressing `T` again all close it.
While the wheel is up the game is held still — no panning, zooming, hotkeys or
card dragging — and everything goes back to normal when it closes.

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

Everything lives in `secret_ways.json`, next to your saves
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
  `Locations` keyed by slot (`swslot1`…`swslot10`) rather than by key, so
  rebinding doesn't orphan anything. Each location's `"Label"` is the name shown
  on the wheel, and editing it here is the same as renaming it with `F2`.
- **`Version`** drives migration. The one migration left runs on a file whose
  `Version` is below 3, and drops the locations it held — those were shared by
  every playthrough, and there's no sound way to say which one they belonged to —
  but the file is copied to `secret_ways.json.pre-v3.bak` first, and your settings
  carry over. Nothing under the mod's old `location_hotkeys.json` name is read any
  more, so a config from before the rename is ignored rather than migrated.

Each playthrough gets its own locations, so a new game starts with nothing bound.
A playthrough is identified by its protagonist rather than by a save file, which
means locations survive reloading and Save As. Entries for abandoned playthroughs
just sit there; delete them by hand, or delete the file to reset everything.

The wheel's photos sit beside the config in `secret_ways_previews/`, one
folder per playthrough and one PNG per slot. They're safe to delete — the wheel
falls back to slot numbers, and `R` takes them again.

## Building

```sh
tools/sync-refs.sh    # copy the game's assemblies into ref/lib (read-only)
tools/build.sh        # compile and stage dist/secret_ways
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
third-party code (`ModManager.Safety.IsDLLAllowed`). Get it from the [Steam
Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3682369347) and
enable it plus "Secret Ways" under Options → Mods.

If you'd rather not go through Steam, `tools/install.sh --enable-dll-mods` writes
a local GHIRBI folder and switches both mods on. Same consequence either way: with
GHIRBI enabled, every enabled DLL mod runs arbitrary code in the game process.

Restart the game after changing any of this — DLLs are loaded once during startup.

## Publishing

There's no external upload tool. The game publishes to the Workshop itself: every
mod it found in your **local** `mods/` folder gets an upload button on its row under
Options → Mods, and pressing it hands the folder to Steam. The button is hidden for
mods that came *from* the Workshop, and hidden unless the game was launched through
Steam — `ModEntry.SetUploadButtonState` checks both.

What Steam ends up showing is drawn straight from the mod folder, so the listing is
edited by editing files here rather than on the web:

| Workshop listing | Comes from |
| --- | --- |
| Title | `synopsis.json` → `name` |
| Description | `synopsis.json` → `description_long` |
| Tags | `synopsis.json` → `tags` |
| Thumbnail | `cover.png` |
| The files themselves | the whole staged folder |

`description_long` is never shown in-game, only on Steam, so it's written as a Steam
page — BBCode and all. It's also **rewritten on every upload**, which cuts both
ways: the file stays the single source of truth, but anything you edit on the web
page is silently reverted the next time you publish. Edit `mod/synopsis.json`.

### cover.png

Required — `tools/build.sh` refuses to stage without it, because the game refuses to
upload without it. A square PNG, 512×512 is plenty, and under 1MB (Steam's limit).
The game also draws it as the icon on the mod's row under Options → Mods, which is
the cheap way to confirm it's where the uploader will look for it: if you can see it
in that list, the upload will find it.

### Screenshots

The game only ever sets the *primary* preview image, from `cover.png` — there's no
way to push screenshots from inside it. Add those on the item's Workshop page, under
the owner controls' *Add/Edit Images & Videos*. Steam keeps additional previews as
separate, indexed entries from the primary one, so uploading again from the game
replaces the thumbnail and leaves the screenshots alone.

The easy way to get them: press Steam's screenshot key (F12 by default) while
playing, which files them in the overlay's screenshot manager ready to attach. The
wheel with a few slots filled in is the shot worth leading with.

### Publishing the first time

```sh
tools/build.sh && tools/install.sh
```

Then launch through Steam, go to Options → Mods, and press upload on the Secret
Ways row. The game creates the item, opens it in the Steam overlay, and writes
its id to `serapeum_catalogue_number.txt` in the *installed* folder. If it's your
first ever upload you'll be told to accept the Workshop terms — the item stays
hidden until you do.

On the item page, set it Public and add **GHIRBI** (`3682369347`) under the owner
controls' *Add/Remove Required Items*. That's a one-time thing; see below.

Then run `tools/install.sh` again. It copies the id back into `mod/` and says so —
commit that file, and consider tagging the commit you published from.

### Updating

Bump `version` in `mod/synopsis.json` *and* `<Version>` in
[SecretWays.csproj](src/SecretWays.csproj) — the second one is what the
mods list shows next to the name, so they should agree. Then it's the same three
steps: build, install, upload.

Because `serapeum_catalogue_number.txt` is now in `mod/`, `build.sh` stages it and
the game updates the existing item instead of making a new one. `install.sh` carries
it in both directions, so neither script's `rm -rf` can lose it.

### Declaring the dependency

The game has no manifest field for prerequisites — `Mod.PopulateFromSynopsis` reads
`name`, `author`, `version`, `description`, `description_long` and `tags`, and drops
anything else on the floor. So GHIRBI is spelled out in four places instead: Steam's
Required Items, the top of `description_long`, the short `description` that shows
in-game, and this README.

Required Items is the only one a machine reads, and it only needs setting once. It's
a property of the published item, not of an upload — `StartItemUpdate` sends title,
description, content, preview and tags and nothing else, so re-publishing can't
clear it.

There's no runtime check and there shouldn't be. Without a gatekeeper enabled the
mod's toggle in Options → Mods isn't even interactive, so `Initialise()` never runs;
a check inside it could never fire.

The game *does* have a content-level `"$depends"` on individual entities
(`EntityDataImportExtensions.DependenciesSatisfied`), but it's the wrong tool. It
skips importing an entity, which would drop the keybind rows while the DLL kept
running — worse than not loading at all. And it matches on `SerapeumCatalogueId`,
which is the id file's contents when there is one and the mod's name otherwise: a
subscribed GHIRBI is `3682369347`, while the local one `install.sh --enable-dll-mods`
writes is `GHIRBI`. Either value breaks one of the two setups.

### Pitfalls

- **Losing the id file makes a duplicate.** No id file means "create a new item", so
  a stray `rm` in `mod/` costs you a second listing with none of the subscribers.
- **Two mods can't share a name.** Once it's published, subscribing to your own item
  gives you a Workshop copy *and* `mods/secret_ways`;
  `ModEntry.ToggleActivation` refuses to enable two mods with the same `name`. Keep
  the local one enabled while developing, and unsubscribe to test the published one.
- **Everything staged is published.** `SetItemContent` uploads the folder wholesale,
  so keep it to `synopsis.json`, `cover.png`, `serapeum_catalogue_number.txt`,
  `content/` and `dll/`. That's what `build.sh` wiping the stage each time is for.
- **Tags have to be ones the game's Workshop knows.** Steam rejects unknown ones. If
  an upload comes back with something other than success, empty `tags` and retry to
  rule it out.
- **Build before you publish.** `dist/` can easily be older than your last commit.

## How it works

`ModManager` loads `dll/SecretWays.dll` and calls the static `Initialise()` on
the global-namespace `SecretWays` class (both names have to match the mod's name
from `synopsis.json` with non-alphanumerics stripped; the loader falls back to
`dll/main.dll` for the file, but nothing saves you on the class). That happens
before the compendium loads and long before any playfield exists, so `Initialise`
just parks a `DontDestroyOnLoad` MonoBehaviour that waits for the game to catch up.

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

- It gets a **nested canvas with `overrideSorting` and a high `sortingOrder`**,
  so it draws over the pop-out panels for verbs and desk actions — those live on
  canvases of their own, which sibling order can't reach past. The game never
  sets a sorting order anywhere in its own code, so nothing of the game's can
  collide. A nested canvas needs its own `GraphicRaycaster` too: graphics
  register against their nearest canvas, so the parent's raycaster stops seeing
  them as soon as one is added.

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
injects actions named `swbindmode` and `swslot1`…`swslot10` into the live
`InputActionAsset`. Get the name wrong and the row silently rebinds `kbfallback`
instead. Because those actions are registered after `ControlsController.Start` has
already replayed saved overrides, `GameBindings` replays its own from each
`Setting.CurrentValue`.

`ref/` is gitignored: `ref/lib/` is assemblies copied out of the game install by
`tools/sync-refs.sh`, `ref/decomp/` is decompiled sources kept for reference.
