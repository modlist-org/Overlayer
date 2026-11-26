#!/usr/bin/env bash
#
# Builds the main mod (Mono and/or IL2CPP).
# Requires Directory.Build.props with O5Dev/game paths (copy from Directory.Build.example.props).
#   ./build.sh [Release_ML|Release_ML_IL2CPP|Debug_ML|Debug_ML_IL2CPP|all] [dotnet build args...]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${1:-Release_ML}"
if [[ $# -gt 0 ]]; then shift; fi

if [[ ! -f "$ROOT/Directory.Build.props" && ! -f "$ROOT/Overlayer/Directory.Build.props" ]]; then
    echo "error: Directory.Build.props not found (checked $ROOT and $ROOT/Overlayer)." >&2
    echo "Copy Directory.Build.example.props to $ROOT/Directory.Build.props first." >&2
    exit 1
fi

if [[ -f "$ROOT/native/build.sh" ]] && command -v zig >/dev/null 2>&1; then
    (cd "$ROOT/native" && ./build.sh)
fi

if [[ "$CONFIG" == "all" ]]; then
    dotnet build "$ROOT/Overlayer/Overlayer.csproj" -c Release_ML "$@"
    dotnet build "$ROOT/Overlayer/Overlayer.csproj" -c Release_ML_IL2CPP "$@"
else
    dotnet build "$ROOT/Overlayer/Overlayer.csproj" -c "$CONFIG" "$@"
fi
