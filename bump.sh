#!/usr/bin/env bash
#
# Sets the version via tool/set_version.sh. The base is the last git tag so a
# release can never be skipped (5.0.3 -> 5.0.5 blocked, 5.0.3 -> 5.1.0 allowed).
# Bypass with --force/-f.
# What changed is shown from git history (commits since the last tag).
#   ./bump.sh patch          # 5.0.3 -> 5.0.4
#   ./bump.sh minor          # 5.0.3 -> 5.1.0
#   ./bump.sh major          # 5.0.3 -> 6.0.0
#   ./bump.sh 5.0.4          # explicit (must be one step from the tag)
#   ./bump.sh patch --force  # ignore the guard
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFO="$ROOT/Overlayer/Core/Info.cs"

FORCE=0
if [[ $# -eq 2 && ("$2" == "--force" || "$2" == "-f") ]]; then
    FORCE=1
elif [[ $# -ne 1 ]]; then
    echo "usage: $0 <patch|minor|major|X.Y.Z> [--force|-f]" >&2
    exit 1
fi
ARG="$1"
MODE=""
if [[ "$ARG" == "patch" || "$ARG" == "minor" || "$ARG" == "major" ]]; then
    MODE="part"
elif [[ "$ARG" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    MODE="explicit"
else
    echo "usage: $0 <patch|minor|major|X.Y.Z> [--force|-f]" >&2
    exit 1
fi

CUR="$(grep -oP '(?<=public const string Version = ")[^"]+' "$INFO" | head -n 1)"
if ! [[ "$CUR" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "error: cannot read current version from $INFO." >&2
    exit 1
fi

LAST_TAG="$(git -C "$ROOT" describe --tags --abbrev=0 2>/dev/null || true)"
BASE="$CUR"
if [[ -n "$LAST_TAG" ]]; then
    TAG_VER="$(git -C "$ROOT" show "$LAST_TAG:Overlayer/Core/Info.cs" 2>/dev/null | grep -oP '(?<=public const string Version = ")[^"]+' | head -n 1 || true)"
    if [[ -n "$TAG_VER" ]]; then
        BASE="$TAG_VER"
    fi
fi

version_gt() {
    local a1 a2 a3 b1 b2 b3
    IFS=. read -r a1 a2 a3 <<< "$1"
    IFS=. read -r b1 b2 b3 <<< "$2"
    if (( a1 > b1 )); then return 0; fi
    if (( a1 < b1 )); then return 1; fi
    if (( a2 > b2 )); then return 0; fi
    if (( a2 < b2 )); then return 1; fi
    if (( a3 > b3 )); then return 0; fi
    return 1
}

if [[ "$MODE" == "part" ]]; then
    IFS=. read -r MA MI PA <<< "$BASE"
    case "$ARG" in
        patch) PA=$((PA + 1)) ;;
        minor) MI=$((MI + 1)); PA=0 ;;
        major) MA=$((MA + 1)); MI=0; PA=0 ;;
    esac
    NEW="$MA.$MI.$PA"
    if [[ "$FORCE" -eq 0 ]] && ! version_gt "$NEW" "$CUR"; then
        echo "error: $NEW would not move past working tree $CUR (last tag: ${LAST_TAG:-none})." >&2
        echo "Pick a bigger part, or retry with --force." >&2
        exit 1
    fi
else
    NEW="$ARG"
    if [[ "$FORCE" -eq 0 ]]; then
        IFS=. read -r MA MI PA <<< "$BASE"
        ALLOWED=("$MA.$MI.$((PA + 1))" "$MA.$((MI + 1)).0" "$((MA + 1)).0.0")
        OK=0
        for v in "${ALLOWED[@]}"; do
            if [[ "$NEW" == "$v" ]]; then OK=1; break; fi
        done
        if [[ "$OK" -eq 0 ]]; then
            echo "error: $NEW is not one step from $BASE (last tag: ${LAST_TAG:-none})." >&2
            echo "allowed: ${ALLOWED[*]} (or retry with --force)" >&2
            exit 1
        fi
    fi
fi

echo "--- changes since last tag (${LAST_TAG:-none}) ---"
if [[ -n "$LAST_TAG" ]]; then
    git -C "$ROOT" log --oneline "${LAST_TAG}..HEAD" || true
else
    git -C "$ROOT" log --oneline -10 || true
fi
echo "--- $CUR -> $NEW ($ARG) ---"

"$ROOT/tool/set_version.sh" "$NEW"

echo "next: git add Overlayer/Core/Info.cs Overlayer/Overlayer.csproj && git commit -m \"$NEW\" && git push"
