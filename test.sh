#!/usr/bin/env bash
#
# Runs the unit tests (no game DLLs required).
#   ./test.sh [dotnet test args...]
#   ./test.sh --filter "FullyQualifiedName~Signature"
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dotnet test "$ROOT/Overlayer.Tests/Overlayer.Tests.csproj" "$@"
