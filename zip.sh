#!/usr/bin/env bash
#
# Assembles release zips from an existing build output (see tool/make_zips.sh).
#   ./zip.sh [.build/Release_ML/Overlayer] [dist]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$ROOT/tool/make_zips.sh" "$@"
