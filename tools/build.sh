#!/usr/bin/env bash
# Build the mod and stage a ready-to-install mod folder in dist/location_hotkeys.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE="$REPO/dist/location_hotkeys"

if [[ ! -f "$REPO/ref/lib/SecretHistories.Main.dll" ]]; then
	echo "ref/lib is empty - run tools/sync-refs.sh first." >&2
	exit 1
fi

# The game refuses to upload a mod without one, and uses it as the mods-list icon.
if [[ ! -f "$REPO/mod/cover.png" ]]; then
	echo "mod/cover.png is missing - the Workshop upload button won't accept the mod." >&2
	echo "Add a square PNG (512x512 is plenty, under 1MB) at mod/cover.png." >&2
	exit 1
fi

dotnet build "$REPO/src/LocationHotkeys.csproj" -c Release -v minimal

rm -rf "$STAGE"
mkdir -p "$STAGE/dll"
cp "$REPO/mod/synopsis.json" "$STAGE/synopsis.json"
cp "$REPO/mod/cover.png" "$STAGE/cover.png"
cp -r "$REPO/mod/content" "$STAGE/content"
cp "$REPO/src/bin/Release/LocationHotkeys.dll" "$STAGE/dll/LocationHotkeys.dll"

# Carrying the published-file id into the stage is what makes an upload update the
# existing Workshop item instead of creating a second one. Absent until first publish.
if [[ -f "$REPO/mod/serapeum_catalogue_number.txt" ]]; then
	cp "$REPO/mod/serapeum_catalogue_number.txt" "$STAGE/serapeum_catalogue_number.txt"
fi

echo
echo "Staged mod folder: $STAGE"
echo "Install it with: tools/install.sh"
