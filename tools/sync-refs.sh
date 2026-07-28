#!/usr/bin/env bash
# Copy the game's managed assemblies into ref/lib so the mod can compile against
# them. Nothing in the game install is modified. Re-run after a game update.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BOH_DIR="${BOH_DIR:-$HOME/.steam/steam/steamapps/common/Book of Hours}"
MANAGED="$BOH_DIR/bh_Data/Managed"

if [[ ! -d "$MANAGED" ]]; then
	echo "Can't find $MANAGED" >&2
	echo "Set BOH_DIR to your Book of Hours install directory." >&2
	exit 1
fi

mkdir -p "$REPO/ref/lib"
cp -r "$MANAGED/." "$REPO/ref/lib/"
echo "Synced $(find "$REPO/ref/lib" -name '*.dll' | wc -l) assemblies from $MANAGED"
