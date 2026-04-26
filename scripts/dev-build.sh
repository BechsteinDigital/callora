#!/usr/bin/env bash
set -euo pipefail

# Local build helper.
#
# Usage:
#   ./scripts/dev-build.sh
#   ./scripts/dev-build.sh --configuration Release
#   ./scripts/dev-build.sh --solution Callora.Host.sln
#   ./scripts/dev-build.sh --skip-admin-ui
#   ./scripts/dev-build.sh --skip-workspace-ui

CONFIGURATION="Debug"
SOLUTION=""
NO_RESTORE=""
SKIP_ADMIN_UI="false"
SKIP_WORKSPACE_UI="false"

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
    --skip-admin-ui)
      SKIP_ADMIN_UI="true"
      shift
      ;;
    --skip-workspace-ui)
      SKIP_WORKSPACE_UI="true"
      shift
      ;;
    *)
      echo "Usage: $0 [--configuration <Debug|Release>] [--solution <path>] [--no-restore] [--skip-admin-ui] [--skip-workspace-ui]"
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

if [[ "$SKIP_ADMIN_UI" != "true" ]]; then
  ./scripts/build-admin-ui.sh
fi

if [[ "$SKIP_WORKSPACE_UI" != "true" ]]; then
  ./scripts/build-workspace-ui.sh
fi
