#!/usr/bin/env bash
#
# Regenerates the CI reference assemblies (tool/build-refs/).
#
# Checked in (plaintext, safe — no game code or game data):
#   build-refs.txt    - assembly identities (name, version, public key token)
#   build-refs.sha256 - SHA-256 of the real game DLL each line was taken from
#   game/...          - stripped reference assemblies: real API surface with
#                       every method body replaced by `ret` / `throw null` and
#                       all managed resources dropped (see tool/GenRefs).
#
# CI (release.yml) copies tool/build-refs/game over the csproj HintPaths and
# builds against it; compilation behaves identically to building against the
# real game install. Re-run this script whenever a game reference DLL is
# added to the csproj, and commit the result.
#
# Usage: ./tool/pack_refs.sh   (needs the game installed at ADOFAI_DIR or
#   ~/.local/share/Steam/steamapps/common/A Dance of Fire and Ice)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAMEPATH="${ADOFAI_DIR:-$HOME/.local/share/Steam/steamapps/common/A Dance of Fire and Ice}"
MANAGED="$GAMEPATH/ADanceOfFireAndIce_Data/Managed"

OUT="$ROOT/tool/build-refs"
mkdir -p "$OUT"

WANT="$OUT/.want.txt"
{
    # MelonLoader/0Harmony for the ML build (keep the subdir: GenRefs
    # resolves them against the game root, not Managed).
    echo "MelonLoader/net35/0Harmony.dll"
    echo "MelonLoader/net35/MelonLoader.dll"
    # Every game-relative HintPath in the csprojs (minus facades the framework provides).
    grep -rhoE '\$\(GamePath\)/\$\(GameData\)/Managed/[A-Za-z0-9._-]+\.dll' "$ROOT/Overlayer/Overlayer.csproj" \
        | sed 's|.*/Managed/||' | sort -u
    # O5Kit's own game refs (usually a subset, but stay exact if it grows).
    if [[ -f "$ROOT/../O5Kit/src/O5Kit/O5Kit.csproj" ]]; then
        grep -rhoE '\$\(GamePath\)/\$\(GameData\)/Managed/[A-Za-z0-9._-]+\.dll' "$ROOT/../O5Kit/src/O5Kit/O5Kit.csproj" \
            | sed 's|.*/Managed/||' | sort -u
    fi
} > "$WANT"
sort -u -o "$WANT" "$WANT"

{
    echo "# Game reference identities for CI (see release.yml)."
    echo "# file|AssemblyName, Version=x[, pkt=HEX]"
} > "$OUT/build-refs.txt"
{
    echo "# SHA-256 of the real game DLL each line above was taken from."
} > "$OUT/build-refs.sha256"

dotnet build "$ROOT/tool/GenRefs/GenRefs.csproj" -c Release --nologo -v q

count=0
while IFS= read -r rel; do
    case "$rel" in
        MelonLoader/*) src="$GAMEPATH/$rel" ;;
        *) src="$MANAGED/$(basename "$rel")" ;;
    esac
    if [[ ! -f "$src" ]]; then
        echo "missing: $src" >&2
        exit 1
    fi
    ident="$(dotnet run --project "$ROOT/tool/GenRefs/GenRefs.csproj" --no-build -c Release -- describe "$src")"
    hash="$(sha256sum "$src" | cut -d' ' -f1)"
    echo "$rel|$ident" >> "$OUT/build-refs.txt"
    echo "$hash  $rel" >> "$OUT/build-refs.sha256"
    count=$((count + 1))
done < "$WANT"
rm -f "$WANT"

# Stripped reference assemblies straight into $OUT/game/... (checked in).
rm -rf "$OUT/game"
GAME_MANAGED_DIR="$MANAGED" dotnet run --project "$ROOT/tool/GenRefs/GenRefs.csproj" --no-build -c Release -- \
    "$OUT/build-refs.txt" "$OUT/build-refs.sha256" "$OUT"

echo "wrote $count entries + stripped refs to $OUT"
