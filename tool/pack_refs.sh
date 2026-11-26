#!/usr/bin/env bash
#
# Regenerates the CI reference assemblies (tool/build-refs/) from sibling O5Dev
# for BOTH Mono (Release_ML) and IL2CPP (Release_ML_IL2CPP).
#
# Checked in:
#   build-refs.txt    - assembly identities (name, version, public key token)
#   build-refs.sha256 - SHA-256 of the O5Dev DLL each line was taken from
#   mono/...          - stripped Mono reference assemblies (O5Dev/Builds/Mono)
#   il2cpp/...        - stripped IL2CPP reference assemblies (O5Dev/Builds/IL2CPP)
#
# Usage: ./tool/pack_refs.sh   (uses O5DEV_DIR or sibling ../O5Dev)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
O5DEV_DIR="${O5DEV_DIR:-$ROOT/../O5Dev}"
BUILDS="$O5DEV_DIR/Builds"
MONO="$BUILDS/Mono"
IL2CPP="$BUILDS/IL2CPP"

if [[ ! -d "$MONO/O5Dev_Data/Managed" ]]; then
    echo "missing Mono O5Dev build: $MONO/O5Dev_Data/Managed (run ./o5dev setup in O5Dev)" >&2
    exit 1
fi
if [[ ! -d "$IL2CPP/MelonLoader/Il2CppAssemblies" ]]; then
    echo "missing IL2CPP O5Dev interop assemblies: $IL2CPP/MelonLoader/Il2CppAssemblies (run ./o5dev run il2cpp --headless --quit-after 180)" >&2
    exit 1
fi

OUT="$ROOT/tool/build-refs"
mkdir -p "$OUT"

CSPROJS=("$ROOT/Overlayer/Overlayer.csproj")
if [[ -f "$ROOT/../O5Kit/src/O5Kit/O5Kit.csproj" ]]; then
    CSPROJS+=("$ROOT/../O5Kit/src/O5Kit/O5Kit.csproj")
fi

WANT="$OUT/.want.txt"
{
    # Mono MelonLoader refs
    echo "mono/MelonLoader/net35/0Harmony.dll"
    echo "mono/MelonLoader/net35/MelonLoader.dll"
    if [[ -f "$MONO/O5Dev_Data/Managed/Newtonsoft.Json.dll" ]]; then
        echo "mono/O5Dev_Data/Managed/Newtonsoft.Json.dll"
    else
        echo "mono/MelonLoader/net35/Newtonsoft.Json.dll"
    fi

    # Mono Unity / Managed compile refs (<HintPath> only; never PostBuild Copy facade DLLs)
    for dll in $(grep -h '<HintPath' "${CSPROJS[@]}" \
        | grep -oE '\$\(GamePath\)/\$\(GameData\)/Managed/[A-Za-z0-9._-]+\.dll' \
        | sed 's|.*/Managed/||' | sort -u); do
        if [[ -f "$MONO/O5Dev_Data/Managed/$dll" ]]; then
            echo "mono/O5Dev_Data/Managed/$dll"
        fi
    done

    # IL2CPP MelonLoader net6 refs
    echo "il2cpp/MelonLoader/net6/0Harmony.dll"
    echo "il2cpp/MelonLoader/net6/Il2CppInterop.Runtime.dll"
    echo "il2cpp/MelonLoader/net6/MelonLoader.dll"
    echo "il2cpp/MelonLoader/net6/Newtonsoft.Json.dll"

    # IL2CPP interop compile refs (<HintPath> only)
    for dll in $(grep -h '<HintPath' "${CSPROJS[@]}" \
        | grep -oE '\$\(Il2CppData\)/[A-Za-z0-9._-]+\.dll' \
        | sed 's|.*/||' | sort -u); do
        if [[ -f "$IL2CPP/MelonLoader/Il2CppAssemblies/$dll" ]]; then
            echo "il2cpp/MelonLoader/Il2CppAssemblies/$dll"
        fi
    done
} > "$WANT"
sort -u -o "$WANT" "$WANT"

{
    echo "# O5Dev Mono + IL2CPP reference identities for CI (see release.yml)."
    echo "# file|AssemblyName, Version=x[, pkt=HEX]"
} > "$OUT/build-refs.txt"
{
    echo "# SHA-256 of the O5Dev DLL each line above was taken from."
} > "$OUT/build-refs.sha256"

dotnet build "$ROOT/tool/GenRefs/GenRefs.csproj" -c Release --nologo -v q

count=0
while IFS= read -r rel; do
    case "$rel" in
        mono/*) src="$MONO/${rel#mono/}" ;;
        il2cpp/*) src="$IL2CPP/${rel#il2cpp/}" ;;
        *) echo "unexpected rel path: $rel" >&2; exit 1 ;;
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

# Stripped reference assemblies straight into $OUT/{mono,il2cpp}/... (checked in).
rm -rf "$OUT/game" "$OUT/mono" "$OUT/il2cpp"
O5DEV_BUILDS_DIR="$BUILDS" dotnet run --project "$ROOT/tool/GenRefs/GenRefs.csproj" --no-build -c Release -- \
    "$OUT/build-refs.txt" "$OUT/build-refs.sha256" "$OUT"

echo "wrote $count O5Dev entries + stripped refs (Mono + IL2CPP) to $OUT"
