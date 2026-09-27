#!/usr/bin/env bash
#
# Assembles the build output (.build/Release_ML/Overlayer) into per-OS release zips.
#   ./tool/make_zips.sh [.build/Release_ML/Overlayer] [dist]
#
# Output:
#   dist/Overlayer_ML_win.zip    (includes ClearScriptV8.win-x64.dll, excludes linux .so)
#   dist/Overlayer_ML_linux.zip  (includes ClearScriptV8.linux-x64.so*, excludes win dll/Registry)
#
# Top-level zip layout: Mods/ UserLibs/ UserData/  (extract straight into the game root)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="${1:-$ROOT/.build/Release_ML/Overlayer}"
DIST="${2:-$ROOT/dist}"

for d in "$SRC/Mods" "$SRC/UserLibs" "$SRC/UserData"; do
    if [[ ! -d "$d" ]]; then
        echo "error: build output missing: $d (run the Release_ML build first)" >&2
        exit 1
    fi
done
mkdir -p "$DIST"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# --- win ---
rm -rf "$WORK/win" && cp -r "$SRC" "$WORK/win"
rm -f "$WORK"/win/UserLibs/ClearScriptV8.linux-x64.so*
(cd "$WORK/win" && zip -qr "$DIST/Overlayer_ML_win.zip" Mods UserData UserLibs)

# --- linux ---
rm -rf "$WORK/linux" && cp -r "$SRC" "$WORK/linux"
rm -f "$WORK"/linux/UserLibs/ClearScriptV8.win-x64.dll \
      "$WORK"/linux/UserLibs/Microsoft.Win32.Registry.dll
(cd "$WORK/linux" && zip -qr "$DIST/Overlayer_ML_linux.zip" Mods UserData UserLibs)

ls -la "$DIST"/Overlayer_ML_win.zip "$DIST"/Overlayer_ML_linux.zip
