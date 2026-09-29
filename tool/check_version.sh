#!/usr/bin/env bash
#
# Prints the game Unity + MelonLoader versions in release-notes format.
# Game path comes from Directory.Build.props (root or Overlayer/) or args.
#   ./tool/check_version.sh [GamePath] [GameData]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROPS="$ROOT/Directory.Build.props"
[[ -f "$PROPS" ]] || PROPS="$ROOT/Overlayer/Directory.Build.props"

GAMEPATH="${1:-$(grep -oP '(?<=<GamePath>).*?(?=</GamePath>)' "$PROPS" 2>/dev/null | head -n 1)}"
GAMEDATA="${2:-$(grep -oP '(?<=<GameData>).*?(?=</GameData>)' "$PROPS" 2>/dev/null | head -n 1)}"
if [[ -z "$GAMEPATH" || -z "$GAMEDATA" ]]; then
    echo "usage: $0 [GamePath] [GameData]" >&2
    exit 1
fi

UNITY_VER=""
shopt -s nullglob
for bin in "$GAMEPATH"/UnityPlayer.* "$GAMEPATH/$GAMEDATA/globalgamemanagers"; do
    [[ -f "$bin" ]] || continue
    UNITY_VER="$(strings "$bin" 2>/dev/null | grep -oE '6000\.[0-9]+\.[0-9]+[a-z][0-9]+|202[0-9]\.[0-9]+\.[0-9]+[a-z][0-9]+' | sort -u | head -n 1 || true)"
    [[ -n "$UNITY_VER" ]] && break
done

ML_VER=""
if [[ -f "$GAMEPATH/MelonLoader/MelonLoader.version" ]]; then
    ML_VER="$(tr -d '[:space:]' < "$GAMEPATH/MelonLoader/MelonLoader.version")"
fi

echo "Build Unity Version : \`${UNITY_VER:-unknown}\`"
echo "ML : \`${ML_VER:-unknown}\`"
