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
#
# Einzelne Ziele — der übliche Fall beim Arbeiten an einer Oberfläche:
#   ./scripts/dev-build.sh --only admin           # nur die Admin-SPA (+ ihr Projekt)
#   ./scripts/dev-build.sh --only surface         # nur die Flächen-Runtime
#   ./scripts/dev-build.sh --only host            # nur .NET, beide Frontends aus
#   ./scripts/dev-build.sh --only plugins         # = --plugins-only
#
# Warum das lohnt: Ein voller Lauf baut beide Vue-Suiten über MSBuild-Targets mit
# (npm ci + vite). Wer eine Zeile im Admin ändert, wartet sonst auch auf die
# Flächen-Runtime, das Plugin-SDK und jedes geklonte Plugin.

CONFIGURATION="Debug"
SOLUTION=""
NO_RESTORE=""
BUILD_SOLUTION=1
BUILD_PLUGINS=1
REPACK=0
NO_FRONTEND=0
ONLY=""
SELECTED_PLUGINS=()

# Ein Ziel ist ein Projekt, kein eigener Buildweg. Die beiden Frontends hängen als
# MSBuild-Target am jeweiligen csproj (BuildAdminFrontend / BuildSurfaceFrontend,
# beide npm ci + npm run build) — deshalb genügt hier ein dotnet build auf genau
# dieses Projekt, und es gibt keine zweite Stelle, an der stünde, wie ein Frontend
# gebaut wird.
only_project() {
  case "$1" in
    admin)   printf 'src/Administration/Callora.Administration.csproj' ;;
    surface) printf 'src/Surface.Rendering/Callora.Surface.Rendering.csproj' ;;
    *)       return 1 ;;
  esac
}

usage() {
  cat <<'EOF'
Usage: dev-build.sh [Optionen]

  -c, --configuration <Debug|Release>   Build-Konfiguration (Default: Debug)
  -s, --solution <pfad>                 Solution-Datei (Default: automatisch)
      --no-restore                      dotnet restore überspringen
      --only <ziel>                     admin | surface | host | plugins
      --skip-plugins                    nur die Solution bauen
      --plugins-only                    nur die Plugins bauen
      --plugins <a,b>                   nur diese pluginIds (mehrfach erlaubt)
      --repack                          local-feed der Plugins neu packen
      --no-frontend                     npm-Schritte der Plugins überspringen
  -h, --help                            diese Hilfe

Ziele für --only:
  admin      src/Administration            Vue-Shell unter /admin
  surface    src/Surface.Rendering         Flächen-Runtime + SSR
  host       Solution ohne beide Frontends braucht kein Node
  plugins    alles unter custom/           wie --plugins-only
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
    --only)
      ONLY="${2:-}"
      case "$ONLY" in
        admin|surface|host|plugins) ;;
        *)
          echo "Unbekanntes Ziel für --only: '${ONLY}'. Erlaubt: admin, surface, host, plugins." >&2
          exit 1
          ;;
      esac
      shift 2
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

EXTRA_BUILD_ARGS=()

# --only wird auf die vorhandenen Schalter abgebildet statt auf einen eigenen
# Zweig: Damit gibt es weiterhin genau einen Weg, die Solution zu bauen, und einen,
# die Plugins zu bauen. Nur admin und surface bauen ein einzelnes Projekt — und
# enden hier, weil danach nichts mehr zu tun ist.
case "$ONLY" in
  admin|surface)
    project="$(only_project "$ONLY")"
    if [[ -z "$NO_RESTORE" ]]; then
      dotnet restore "$project"
    fi
    dotnet build "$project" --configuration "$CONFIGURATION" $NO_RESTORE --nologo --verbosity minimal
    exit 0
    ;;
  host)
    # Ohne Node bauen: Beide Frontend-Targets tragen genau dieses Opt-out. Ein
    # Host-Build ohne sie liefert eine .NET-Fläche, die kompiliert — die zuletzt
    # gebauten Bundles bleiben liegen, statt neu zu entstehen.
    BUILD_PLUGINS=0
    EXTRA_BUILD_ARGS+=(-p:SkipAdminFrontend=true -p:SkipSurfaceFrontend=true)
    ;;
  plugins)
    BUILD_SOLUTION=0
    ;;
esac

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

  dotnet build "$SOLUTION" --configuration "$CONFIGURATION" $NO_RESTORE \
    "${EXTRA_BUILD_ARGS[@]+"${EXTRA_BUILD_ARGS[@]}"}" --nologo --verbosity minimal
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
