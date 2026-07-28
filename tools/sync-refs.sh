#!/usr/bin/env bash
# Copy the game's managed assemblies into ref/lib so the mod can compile against
# them. Nothing in the game install is modified. Re-run after a game update.
#
# Usage: tools/sync-refs.sh [GAME_DIR]
#   GAME_DIR defaults to $BOH_DIR, then to the usual Steam library locations.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

CANDIDATES=(
	"$HOME/.steam/steam/steamapps/common/Book of Hours"
	"$HOME/.local/share/Steam/steamapps/common/Book of Hours"
	"$HOME/Library/Application Support/Steam/steamapps/common/Book of Hours"
	"/c/Program Files (x86)/Steam/steamapps/common/Book of Hours"
)

GAME="${1:-${BOH_DIR:-}}"
if [[ -z "$GAME" ]]; then
	for dir in "${CANDIDATES[@]}"; do
		if [[ -d "$dir/bh_Data/Managed" ]]; then
			GAME="$dir"
			break
		fi
	done
fi

MANAGED="$GAME/bh_Data/Managed"

if [[ -z "$GAME" || ! -d "$MANAGED" ]]; then
	echo "Can't find the game's assemblies${GAME:+ at $MANAGED}." >&2
	echo "Pass your Book of Hours install directory: tools/sync-refs.sh /path/to/Book of Hours" >&2
	echo "(or set BOH_DIR). It's the folder containing bh_Data/." >&2
	exit 1
fi

mkdir -p "$REPO/ref/lib"
cp -r "$MANAGED/." "$REPO/ref/lib/"
echo "Synced $(find "$REPO/ref/lib" -name '*.dll' | wc -l) assemblies from $MANAGED"
