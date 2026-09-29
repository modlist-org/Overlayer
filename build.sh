#!/usr/bin/env bash
#
# Builds the main mod.
# Requires Directory.Build.props with GamePath (copy from Directory.Build.example.props).
#   ./build.sh [Release_ML|Debug_ML|...] [dotnet build args...]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${1:-Release_ML}"
if [[ $# -gt 0 ]]; then shift; fi

if [[ ! -f "$ROOT/Directory.Build.props" && ! -f "$ROOT/Overlayer/Directory.Build.props" ]]; then
    echo "error: Directory.Build.props not found (checked $ROOT and $ROOT/Overlayer)." >&2
    echo "Copy Directory.Build.example.props to $ROOT/Directory.Build.props and set GamePath first." >&2
    exit 1
fi

dotnet build "$ROOT/Overlayer/Overlayer.csproj" -c "$CONFIG" "$@"
