#!/usr/bin/env bash
# Copy the staged mod into the game's local mods folder.
#
# Book of Hours refuses to load any DLL mod unless a "gatekeeper" mod named
# GHIRBI is installed and enabled - that is the game's own consent gate for
# running third-party code. Pass --enable-dll-mods to create a local GHIRBI
# folder and switch both mods on without going through the in-game menu.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE="$REPO/dist/location_hotkeys"
DATA="${BOH_DATA_DIR:-$HOME/.config/unity3d/Weather Factory/Book of Hours}"
MODS="$DATA/mods"

ENABLE=0
[[ "${1:-}" == "--enable-dll-mods" ]] && ENABLE=1

if [[ ! -d "$STAGE" ]]; then
	echo "Nothing staged - run tools/build.sh first." >&2
	exit 1
fi

if [[ ! -d "$DATA" ]]; then
	echo "Can't find the game's save folder at $DATA" >&2
	echo "Set BOH_DATA_DIR, or find it via the game's Options > BROWSE FILES." >&2
	exit 1
fi

mkdir -p "$MODS"
rm -rf "$MODS/location_hotkeys"
cp -r "$STAGE" "$MODS/location_hotkeys"
echo "Installed $MODS/location_hotkeys"

if [[ $ENABLE -eq 0 ]]; then
	cat <<EOF

Not enabled yet. Either:
  - enable "Location Hotkeys" and the GHIRBI gatekeeper in-game under
    Options > Mods (GHIRBI is on the Steam Workshop), or
  - re-run this script as: tools/install.sh --enable-dll-mods
EOF
	exit 0
fi

# The gatekeeper is matched on its name and description verbatim; see
# ModManager.Safety.IsGatekeeper in the decompiled game code.
mkdir -p "$MODS/ghirbi"
cat > "$MODS/ghirbi/synopsis.json" <<'EOF'
{
    "name": "GHIRBI",
    "author": "Weather Factory",
    "version": "1.0.0",

    "description": "WARNING! Enabling this permits execution of third-party code by DLL mods. Use only trusted DLL mods; and still at your own risk.",
    "description_long": "Local copy of the gatekeeper mod that unlocks DLL modding.",
    "tags": [ "Utility" ]
}
EOF

# mods.txt is one enabled mod id (folder name) per line.
ENABLED="$DATA/mods.txt"
touch "$ENABLED"
for id in ghirbi location_hotkeys; do
	grep -qxF "$id" "$ENABLED" || echo "$id" >> "$ENABLED"
done

cat <<EOF

Enabled GHIRBI + Location Hotkeys in $ENABLED

  GHIRBI is the game's consent gate for DLL mods. With it on, any enabled DLL
  mod runs arbitrary code in the game process. Turn it off under Options > Mods
  if you stop wanting that.

Restart Book of Hours for the changes to take effect.
EOF
