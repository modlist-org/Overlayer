#!/usr/bin/env bash
#
# Injects the tag version (X.Y.Z) into the code.
#   ./tool/set_version.sh 5.0.3
#
# Updated files:
#   - Overlayer/Core/Info.cs          -> public const string Version = "X.Y.Z"
#   - Overlayer/Overlayer.csproj      -> <AssemblyVersion>X.Y.Z.0</AssemblyVersion>
#                                        <FileVersion>X.Y.Z.0</FileVersion>
#
# NOTE: numeric X.Y.Z only. Do NOT use suffixes like "-beta.1".
#   OverlayerRuntime parses Info.Version with new Version(), which throws on suffixes.
#   Beta vs stable is distinguished via the GitHub Release prerelease flag instead.
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "usage: $0 <X.Y.Z>" >&2
    exit 1
fi
VER="$1"
if ! [[ "$VER" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "error: version must be X.Y.Z (e.g. 5.0.3), got: $VER" >&2
    exit 1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INFO="$ROOT/Overlayer/Core/Info.cs"
CSPROJ="$ROOT/Overlayer/Overlayer.csproj"

sed -i -E "s/public const string Version = \"[^\"]+\";/public const string Version = \"$VER\";/" "$INFO"
sed -i -E "s|<AssemblyVersion>[^<]+</AssemblyVersion>|<AssemblyVersion>${VER}.0</AssemblyVersion>|" "$CSPROJ"
sed -i -E "s|<FileVersion>[^<]+</FileVersion>|<FileVersion>${VER}.0</FileVersion>|" "$CSPROJ"

echo "--- Info.cs ---"
grep -n 'public const string Version' "$INFO"
echo "--- Overlayer.csproj ---"
grep -n -E '<(Assembly|File)Version>' "$CSPROJ"
