#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$HERE"
TARGET="${1:-}"
if [[ -n "$TARGET" ]]; then
    zig build --release=fast -Dtarget="$TARGET" --prefix "$HERE/zig-out"
    TRIPLE="$TARGET"
else
    zig build --release=fast --prefix "$HERE/zig-out"
    TRIPLE="$(uname -s)"
fi

OS="$(echo "$TRIPLE" | cut -d- -f2 2>/dev/null || true)"
[[ -z "$OS" ]] && OS="$TRIPLE"
OS="$(echo "$OS" | tr '[:upper:]' '[:lower:]')"
case "$OS" in
    linux*) OS="linux"; EXT="so"; SRC="liboverlayernative.so" ;;
    windows*|mingw*) OS="win"; EXT="dll"; SRC="overlayernative.dll" ;;
    darwin*|macos*) OS="mac"; EXT="dylib"; SRC="liboverlayernative.dylib" ;;
    *) echo "unknown os in $TRIPLE" >&2; exit 1 ;;
esac

mkdir -p "$HERE/zig-out/lib"
if [[ -f "$HERE/zig-out/lib/$SRC" ]]; then
    mv -f "$HERE/zig-out/lib/$SRC" "$HERE/zig-out/lib/OverlayerNative.$OS.$EXT"
elif [[ -f "$HERE/zig-out/bin/$SRC" ]]; then
    mv -f "$HERE/zig-out/bin/$SRC" "$HERE/zig-out/lib/OverlayerNative.$OS.$EXT"
fi
ls -la "$HERE/zig-out/lib"
