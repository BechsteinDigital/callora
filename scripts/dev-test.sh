#!/usr/bin/env bash
set -euo pipefail

# Local test helper.
#
# Usage:
#   ./scripts/dev-test.sh
#   ./scripts/dev-test.sh --project tests/Callora.Host.Backend.Tests/Callora.Host.Backend.Tests.csproj
#   ./scripts/dev-test.sh --filter "FullyQualifiedName~Plugin"
#   ./scripts/dev-test.sh --configuration Release --no-build

CONFIGURATION="Debug"
SOLUTION_OR_PROJECT=""
FILTER=""
NO_BUILD=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --project|-p|--solution|-s)
      SOLUTION_OR_PROJECT="${2:-}"
      shift 2
      ;;
    --filter|-f)
      FILTER="${2:-}"
      shift 2
      ;;
    --configuration|-c)
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    --no-build)
      NO_BUILD="--no-build"
      shift
      ;;
    *)
      echo "Usage: $0 [--project <path>|--solution <path>] [--filter <expr>] [--configuration <Debug|Release>] [--no-build]"
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ -z "$SOLUTION_OR_PROJECT" ]]; then
  if [[ -f "Callora.Host.sln" ]]; then
    SOLUTION_OR_PROJECT="Callora.Host.sln"
  else
    echo "No solution file found (expected Callora.Host.sln)."
    exit 1
  fi
fi

ARGS=(dotnet test "$SOLUTION_OR_PROJECT" --configuration "$CONFIGURATION" $NO_BUILD --nologo --verbosity minimal)
if [[ -n "$FILTER" ]]; then
  ARGS+=(--filter "$FILTER")
fi

"${ARGS[@]}"
