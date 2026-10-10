#!/usr/bin/env bash
#
# Assembles the build output (.build/Release_ML/Overlayer) into per-OS release zips.
#   ./tool/make_zips.sh [.build/Release_ML/Overlayer] [dist]
#
# Output:
#   dist/Overlayer_ML_win.zip    (includes ClearScriptV8.win-x64.dll, excludes linux .so / osx dylib)
#   dist/Overlayer_ML_linux.zip  (includes linux .so + win dll for Proton/Wine titles, excludes osx dylib)
#   dist/Overlayer_ML_mac.zip    (includes osx dylib + win dll for Wine/Crossover titles, excludes linux .so)
#
# NOTE: NFD natives (nfd.dll / libnfd.so / libnfd.dylib, ~50KB total) are shipped in
# every zip: Proton/Wine games load nfd.dll even when the host OS is Linux/Mac.
# Likewise the win ClearScript dll is kept in the linux/mac zips so Proton titles work.
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
rm -f "$DIST"/Overlayer_ML_*.zip

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# --- win ---
rm -rf "$WORK/win" && cp -r "$SRC" "$WORK/win"
rm -f "$WORK"/win/UserLibs/*.so* "$WORK"/win/UserLibs/*.dylib
(cd "$WORK/win" && zip -qr "$DIST/Overlayer_ML_win.zip" Mods UserData UserLibs)

# --- linux ---
# Keep ClearScriptV8.win-x64.dll: Proton/Wine titles on Linux are Windows processes
# and need the win build (same reason all NFD natives are kept).
rm -rf "$WORK/linux" && cp -r "$SRC" "$WORK/linux"
rm -f "$WORK"/linux/UserLibs/Microsoft.Win32.Registry.dll \
      "$WORK"/linux/UserLibs/ClearScriptV8.osx-*.dylib
(cd "$WORK/linux" && zip -qr "$DIST/Overlayer_ML_linux.zip" Mods UserData UserLibs)

# --- mac ---
# Keep ClearScriptV8.win-x64.dll for Wine/Crossover titles; NFD natives all kept.
rm -rf "$WORK/mac" && cp -r "$SRC" "$WORK/mac"
rm -f "$WORK"/mac/UserLibs/Microsoft.Win32.Registry.dll \
      "$WORK"/mac/UserLibs/ClearScriptV8.linux-x64.so*
(cd "$WORK/mac" && zip -qr "$DIST/Overlayer_ML_mac.zip" Mods UserData UserLibs)

ls -la "$DIST"/Overlayer_ML_win.zip "$DIST"/Overlayer_ML_linux.zip "$DIST"/Overlayer_ML_mac.zip
