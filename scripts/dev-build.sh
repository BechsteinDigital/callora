#!/usr/bin/env bash
set -euo pipefail

# Local build helper. Builds the solution (the colocated admin/surface UIs build as
# part of the .NET build via their MSBuild targets — no separate UI step).
#
# Usage:
#   ./scripts/dev-build.sh
#   ./scripts/dev-build.sh --configuration Release
#   ./scripts/dev-build.sh --solution Callora.Host.sln

CONFIGURATION="Debug"
SOLUTION=""
NO_RESTORE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration|-c)
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    --solution|-s)
      SOLUTION="${2:-}"
      shift 2
      ;;
    --no-restore)
      NO_RESTORE="--no-restore"
      shift
      ;;
    *)
      echo "Usage: $0 [--configuration <Debug|Release>] [--solution <path>] [--no-restore]"
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ -z "$SOLUTION" ]]; then
  if [[ -f "Callora.Host.sln" ]]; then
    SOLUTION="Callora.Host.sln"
  elif [[ -f "Callora.sln" ]]; then
    SOLUTION="Callora.sln"
  elif [[ -f "VoipSdk.sln" ]]; then
    SOLUTION="VoipSdk.sln"
  else
    echo "No solution file found (expected Callora.Host.sln, Callora.sln or VoipSdk.sln)."
    exit 1
  fi
fi

if [[ -z "$NO_RESTORE" ]]; then
  dotnet restore "$SOLUTION"
fi

dotnet build "$SOLUTION" --configuration "$CONFIGURATION" $NO_RESTORE --nologo --verbosity minimal
