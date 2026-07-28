#!/usr/bin/env bash
# Build the mod and stage a ready-to-install mod folder in dist/location_hotkeys.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE="$REPO/dist/location_hotkeys"

if [[ ! -f "$REPO/ref/lib/SecretHistories.Main.dll" ]]; then
	echo "ref/lib is empty - run tools/sync-refs.sh first." >&2
	exit 1
fi

dotnet build "$REPO/src/LocationHotkeys.csproj" -c Release -v minimal

rm -rf "$STAGE"
mkdir -p "$STAGE/dll"
cp "$REPO/mod/synopsis.json" "$STAGE/synopsis.json"
cp "$REPO/src/bin/Release/LocationHotkeys.dll" "$STAGE/dll/LocationHotkeys.dll"

echo
echo "Staged mod folder: $STAGE"
echo "Install it with: tools/install.sh"
