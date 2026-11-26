#!/usr/bin/env bash
#
# Assembles Mono (Release_ML) and IL2CPP (Release_ML_IL2CPP) build outputs into per-OS release zips.
#
# Usage:
#   ./tool/make_zips.sh [--all|--mono|--il2cpp] [dist]
#   ./tool/make_zips.sh <src_dir> [dist] [prefix]   # single-variant legacy mode
#
# Output (default --all):
#   dist/Overlayer_ML_win.zip
#   dist/Overlayer_ML_linux.zip
#   dist/Overlayer_ML_mac.zip
#   dist/Overlayer_ML_IL2CPP_win.zip
#   dist/Overlayer_ML_IL2CPP_linux.zip
#   dist/Overlayer_ML_IL2CPP_mac.zip
#
# NOTE: NFD natives (nfd.dll / libnfd.so / libnfd.dylib) and ClearScriptV8.win-x64.dll
# are kept in linux/mac zips so Proton/Wine titles work out of the box.
# Top-level zip layout: Mods/ UserLibs/ UserData/  (extract straight into the game root)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="--all"
DIST="$ROOT/dist"
LEGACY_SRC=""
LEGACY_PREFIX="Overlayer_ML"

case "${1:-}" in
    --all|--mono|--il2cpp)
        MODE="$1"
        DIST="${2:-$ROOT/dist}"
        ;;
    "")
        ;;
    *)
        MODE="legacy"
        LEGACY_SRC="$1"
        DIST="${2:-$ROOT/dist}"
        LEGACY_PREFIX="${3:-Overlayer_ML}"
        ;;
esac

mkdir -p "$DIST"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

pack_variant() {
    local src="$1"
    local prefix="$2"

    for d in "$src/Mods" "$src/UserLibs" "$src/UserData"; do
        if [[ ! -d "$d" ]]; then
            echo "error: build output missing: $d (run dotnet build first)" >&2
            exit 1
        fi
    done

    rm -f "$DIST/${prefix}_win.zip" "$DIST/${prefix}_linux.zip" "$DIST/${prefix}_mac.zip"

    # --- win ---
    rm -rf "$WORK/win" && cp -r "$src" "$WORK/win"
    rm -f "$WORK"/win/UserLibs/*.so* "$WORK"/win/UserLibs/*.dylib "$WORK"/win/UserLibs/O5Kit.dll
    (cd "$WORK/win" && zip -qr "$DIST/${prefix}_win.zip" Mods UserData UserLibs)

    # --- linux ---
    # Keep ClearScriptV8.win-x64.dll + nfd.dll for Proton/Wine titles on Linux.
    rm -rf "$WORK/linux" && cp -r "$src" "$WORK/linux"
    rm -f "$WORK"/linux/UserLibs/Microsoft.Win32.Registry.dll \
          "$WORK"/linux/UserLibs/ClearScriptV8.osx-*.dylib \
          "$WORK"/linux/UserLibs/*Native.mac.dylib \
          "$WORK"/linux/UserLibs/libo5native.dylib \
          "$WORK"/linux/UserLibs/liboverlayernative.dylib \
          "$WORK"/linux/UserLibs/System.Security.Cryptography.Native.Apple.dylib \
          "$WORK"/linux/UserLibs/O5Kit.dll
    (cd "$WORK/linux" && zip -qr "$DIST/${prefix}_linux.zip" Mods UserData UserLibs)

    # --- mac ---
    # Keep ClearScriptV8.win-x64.dll + nfd.dll for Wine/Crossover titles on macOS.
    rm -rf "$WORK/mac" && cp -r "$src" "$WORK/mac"
    rm -f "$WORK"/mac/UserLibs/Microsoft.Win32.Registry.dll \
          "$WORK"/mac/UserLibs/ClearScriptV8.linux-x64.so* \
          "$WORK"/mac/UserLibs/*Native.linux.so* \
          "$WORK"/mac/UserLibs/libo5native.so* \
          "$WORK"/mac/UserLibs/liboverlayernative.so* \
          "$WORK"/mac/UserLibs/System.Security.Cryptography.Native.OpenSsl.so* \
          "$WORK"/mac/UserLibs/O5Kit.dll
    (cd "$WORK/mac" && zip -qr "$DIST/${prefix}_mac.zip" Mods UserData UserLibs)

    ls -la "$DIST/${prefix}_win.zip" "$DIST/${prefix}_linux.zip" "$DIST/${prefix}_mac.zip"
}

case "$MODE" in
    --all)
        pack_variant "$ROOT/.build/Release_ML/Overlayer" "Overlayer_ML"
        pack_variant "$ROOT/.build/Release_ML_IL2CPP/Overlayer" "Overlayer_ML_IL2CPP"
        ;;
    --mono)
        pack_variant "$ROOT/.build/Release_ML/Overlayer" "Overlayer_ML"
        ;;
    --il2cpp)
        pack_variant "$ROOT/.build/Release_ML_IL2CPP/Overlayer" "Overlayer_ML_IL2CPP"
        ;;
    legacy)
        pack_variant "$LEGACY_SRC" "$LEGACY_PREFIX"
        ;;
esac
