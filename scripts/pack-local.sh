#!/usr/bin/env bash
# Baustein 1 (Callora-Production-Setup): packt die Framework-Module + den
# Communication-SDK-Contract als .nupkg in einen lokalen Feed.
#
# Feste Version 0.1.0-local (überschreibbar als $1), damit Konsumenten
# (Callora-Production, Durchstich-Host) stabil referenzieren können — MinVer
# erzeugt sonst pro Commit eine wandernde Höhe (0.1.0-preview.0.<N>).
#
#   scripts/pack-local.sh            # → 0.1.0-local
#   scripts/pack-local.sh 0.2.0-rc1  # → eigene Version
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FEED="$REPO_ROOT/artifacts/nuget-local"
VERSION="${1:-0.1.0-local}"

PROJECTS=(
  "src/Core/Callora.Core.csproj"
  "src/Administration/Callora.Administration.csproj"
  "src/Workspace/Callora.Workspace.csproj"
  "custom/static-plugins/Communication/Abstractions/Callora.Plugin.Communication.Abstractions.csproj"
)

echo "→ Feed: $FEED (Version $VERSION)"
rm -rf "$FEED"
mkdir -p "$FEED"

for proj in "${PROJECTS[@]}"; do
  echo "→ pack $proj"
  dotnet pack "$REPO_ROOT/$proj" -c Release \
    -p:MinVerVersionOverride="$VERSION" \
    -o "$FEED"
done

echo "→ fertig. Pakete im Feed:"
ls -1 "$FEED"/*.nupkg
