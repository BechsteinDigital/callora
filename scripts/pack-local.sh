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
  # Das SDK, gegen das JEDES Plugin baut. Es fehlte hier, und der Feed war damit unbrauchbar
  # für genau den Zweck, für den er existiert: Communication, Composer und VideoConference
  # referenzieren alle Callora.Plugin.Sdk, fanden nur die vorige Version und bauten deshalb
  # weiter gegen einen älteren Vertrag als der Host bereitstellt — bis eine Signatur sich
  # änderte und das Plugin beim Laden ausfiel.
  "src/Plugin.Sdk/Callora.Plugin.Sdk.csproj"
  # Der Governance-Analyzer (CAL0001-0004) reist als Paket mit, weil jedes Plugin-Repo ihn
  # referenziert — ohne ihn im Feed scheitert dort schon der Restore.
  "src/Analyzers/Callora.Analyzers.csproj"
  "src/Administration/Callora.Administration.csproj"
  "src/Workspace/Callora.Workspace.csproj"
  "src/Surface.Rendering/Callora.Surface.Rendering.csproj"
  "custom/static-plugins/communication/src/Abstractions/Callora.Plugin.Communication.Abstractions.csproj"
)

echo "→ Feed: $FEED (Version $VERSION)"
rm -rf "$FEED"
mkdir -p "$FEED"

for proj in "${PROJECTS[@]}"; do
  echo "→ pack $proj"
  dotnet pack "$REPO_ROOT/$proj" -c Release \
    --disable-build-servers \
    -m:1 \
    --tl:off \
    -p:MinVerVersionOverride="$VERSION" \
    -o "$FEED"
done

echo "→ fertig. Pakete im Feed:"
ls -1 "$FEED"/*.nupkg
