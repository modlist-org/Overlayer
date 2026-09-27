#!/usr/bin/env bash
#
# Collects the reference DLLs needed for CI builds into an encrypted
# zip (tool/build-refs.zip.enc).
# Game DLLs must never be committed in plaintext (copyright) -> encrypt with
# openssl AES-256-CBC and commit the .enc file. The decryption key lives only
# in GitHub Secrets (REFS_KEY).
#
# Usage (once at setup + whenever the referenced DLL set changes):
#   openssl rand -hex 32              # generate key -> store it somewhere safe
#   REFS_KEY=<key> ./tool/pack_refs.sh
#   # commit tool/build-refs.zip.enc
#   # register REFS_KEY under GitHub repo Settings > Secrets > Actions
#
# Contents = files referenced by Overlayer.csproj that come from the game folder:
#   MelonLoader/net35/{0Harmony,MelonLoader}.dll
#   <GameData>/Managed/{DemiLib, I18N*, Newtonsoft.Json, Rewired_Core, Unity*, UnityEngine*}.dll
# NOTE: re-run this script whenever a new game reference DLL is added to the csproj.
set -euo pipefail

: "${REFS_KEY:?error: REFS_KEY env is required. Generate one with 'openssl rand -hex 32'.}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROPS="$ROOT/Directory.Build.props"
GAMEPATH="$(grep -oP '(?<=<GamePath>).*?(?=</GamePath>)' "$PROPS" | head -n 1)"
GAMEDATA="$(grep -oP '(?<=<GameData>).*?(?=</GameData>)' "$PROPS" | head -n 1)"
if [[ -z "$GAMEPATH" || -z "$GAMEDATA" ]]; then
    echo "error: could not read GamePath/GameData from Directory.Build.props." >&2
    exit 1
fi

MANAGED="$GAMEPATH/$GAMEDATA/Managed"
NET35="$GAMEPATH/MelonLoader/net35"

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/game/GameData/Managed" "$STAGE/game/MelonLoader/net35"

shopt -s nullglob
MANAGED_FILES=(
    "$MANAGED/DemiLib.dll"
    "$MANAGED"/I18N*.dll
    "$MANAGED/Newtonsoft.Json.dll"
    "$MANAGED/Rewired_Core.dll"
    "$MANAGED"/Unity.*.dll
    "$MANAGED"/UnityEngine*.dll
)
if [[ ${#MANAGED_FILES[@]} -eq 0 ]]; then
    echo "error: no reference DLLs found in $MANAGED." >&2
    exit 1
fi
cp -v "${MANAGED_FILES[@]}" "$STAGE/game/GameData/Managed/"
cp -v "$NET35/0Harmony.dll" "$NET35/MelonLoader.dll" "$STAGE/game/MelonLoader/net35/"

echo "packed ${#MANAGED_FILES[@]} managed + 2 melonloader dlls"

(
    cd "$STAGE"
    zip -qr "$ROOT/tool/build-refs.zip" game
)
openssl enc -aes-256-cbc -pbkdf2 \
    -in "$ROOT/tool/build-refs.zip" \
    -out "$ROOT/tool/build-refs.zip.enc" \
    -pass env:REFS_KEY
rm -f "$ROOT/tool/build-refs.zip"  # never leave the plaintext behind

ls -la "$ROOT/tool/build-refs.zip.enc"
echo "done. Commit tool/build-refs.zip.enc and register REFS_KEY in GitHub Secrets."
