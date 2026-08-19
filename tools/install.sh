#!/usr/bin/env bash
# Copy the staged mod into the game's local mods folder.
#
# Usage: tools/install.sh [DATA_DIR]
#   DATA_DIR is the game's save folder - the one holding mods/ and mods.txt.
#   It defaults to $BOH_DATA_DIR, then to the usual per-platform location.
#   Options > BROWSE FILES in-game opens it.
#
# Enabling the mod is a separate, in-game step: Options > Mods, where both Secret
# Ways and the GHIRBI gatekeeper have to be switched on.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE="$REPO/dist/secret_ways"

DATA="${1:-${BOH_DATA_DIR:-$HOME/.config/unity3d/Weather Factory/Book of Hours}}"
MODS="$DATA/mods"

if [[ ! -d "$STAGE" ]]; then
	echo "Nothing staged - run tools/build.sh first." >&2
	exit 1
fi

if [[ ! -d "$DATA" ]]; then
	echo "Can't find the game's save folder at $DATA" >&2
	echo "Pass it as an argument: tools/install.sh '/path/to/Book of Hours'" >&2
	echo "Options > BROWSE FILES in-game opens the right folder." >&2
	exit 1
fi

mkdir -p "$MODS"

# The game writes the Workshop published-file id into the installed folder when you
# upload from Options > Mods. Rescue it into the repo before the wipe below, or the
# next upload creates a second Workshop item instead of updating the first.
CATALOGUE="serapeum_catalogue_number.txt"
if [[ -f "$MODS/secret_ways/$CATALOGUE" && ! -f "$REPO/mod/$CATALOGUE" ]]; then
	cp "$MODS/secret_ways/$CATALOGUE" "$REPO/mod/$CATALOGUE"
	echo "Recorded Workshop item id $(cat "$REPO/mod/$CATALOGUE") in mod/$CATALOGUE - commit it."
fi

rm -rf "$MODS/secret_ways"
cp -r "$STAGE" "$MODS/secret_ways"

# Keeps the install correct when run against a stage built before the id existed.
if [[ -f "$REPO/mod/$CATALOGUE" ]]; then
	cp "$REPO/mod/$CATALOGUE" "$MODS/secret_ways/$CATALOGUE"
fi

echo "Installed $MODS/secret_ways"
cat <<EOF

Not enabled yet. Enable "Secret Ways" and the GHIRBI gatekeeper in-game under
Options > Mods (GHIRBI is on the Steam Workshop:
https://steamcommunity.com/sharedfiles/filedetails/?id=3682369347).

Restart Book of Hours afterwards - DLLs are loaded once during startup.
EOF
