#!/usr/bin/env bash
set -euo pipefail

# Local build helper. Builds the solution (the colocated admin/surface UIs build as
# part of the .NET build via their MSBuild targets — no separate UI step) and then
# every plugin that lies in the discovery folders.
#
# Der Plugin-Teil hat keine Liste: gebaut wird, was der Host findet — jede
# registry.json unter custom/static-plugins und custom/plugins. Ein neu
# dazugeklontes Plugin ist damit automatisch dabei (scripts/lib/dev-plugins.sh).
#
# Usage:
#   ./scripts/dev-build.sh                        # Solution + alle Plugins
#   ./scripts/dev-build.sh --skip-plugins         # nur die Solution (wie früher)
#   ./scripts/dev-build.sh --plugins-only         # nur die Plugins
#   ./scripts/dev-build.sh --plugins composer     # nur benannte (Komma oder mehrfach)
#   ./scripts/dev-build.sh --repack               # Plattform-Pakete der Plugins erneuern
#   ./scripts/dev-build.sh --no-frontend          # npm-Schritte überspringen
#   ./scripts/dev-build.sh --configuration Release
#   ./scripts/dev-build.sh --solution Callora.Host.sln

CONFIGURATION="Debug"
SOLUTION=""
NO_RESTORE=""
BUILD_SOLUTION=1
BUILD_PLUGINS=1
REPACK=0
NO_FRONTEND=0
SELECTED_PLUGINS=()

usage() {
  cat <<'EOF'
Usage: dev-build.sh [Optionen]

  -c, --configuration <Debug|Release>   Build-Konfiguration (Default: Debug)
  -s, --solution <pfad>                 Solution-Datei (Default: automatisch)
      --no-restore                      dotnet restore überspringen
      --skip-plugins                    nur die Solution bauen
      --plugins-only                    nur die Plugins bauen
      --plugins <a,b>                   nur diese pluginIds (mehrfach erlaubt)
      --repack                          local-feed der Plugins neu packen
      --no-frontend                     npm-Schritte der Plugins überspringen
  -h, --help                            diese Hilfe
EOF
}

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
    --skip-plugins)
      BUILD_PLUGINS=0
      shift
      ;;
    --plugins-only)
      BUILD_SOLUTION=0
      shift
      ;;
    --plugins)
      # Komma oder Leerzeichen getrennt, mehrfach angebbar.
      IFS=', ' read -r -a _names <<<"${2:-}"
      SELECTED_PLUGINS+=("${_names[@]}")
      shift 2
      ;;
    --no-frontend)
      NO_FRONTEND=1
      shift
      ;;
    --repack)
      REPACK=1
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      usage >&2
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ "$BUILD_SOLUTION" == "1" ]]; then
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
fi

if [[ "$BUILD_PLUGINS" == "1" ]]; then
  # shellcheck source=lib/dev-plugins.sh
  source "$ROOT_DIR/scripts/lib/dev-plugins.sh"

  DEV_PLUGINS_ROOT_DIR="$ROOT_DIR"
  DEV_PLUGINS_CONFIG="$CONFIGURATION"
  DEV_PLUGINS_REPACK="$REPACK"
  DEV_PLUGINS_NO_FRONTEND="$NO_FRONTEND"
  DEV_PLUGINS_SELECTED=("${SELECTED_PLUGINS[@]+"${SELECTED_PLUGINS[@]}"}")

  dev_plugins_run
fi
