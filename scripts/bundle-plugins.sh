#!/usr/bin/env bash
# Baut, bündelt und signiert die First-Party-Plugins nach custom/{static-,}plugins/, damit der
# Host sie zur Laufzeit findet und verifiziert. Wie der lokale NuGet-Feed ist die Ausgabe ein
# gitignoriertes Build-Artefakt — committet wird nur der ÖFFENTLICHE Schlüssel des Signierers
# (appsettings BackendHost:TrustedSigners).
#
# WICHTIG: Der Manifest-Verifier braucht den öffentlichen Schlüssel (PEM), um die
# ECDSA-P256-Signatur zu prüfen. Ein reiner Fingerprint (TrustedSignerThumbprints) ist
# fail-closed und reicht NICHT.
#
# Jedes Plugin lebt in einem EIGENEN Repository — hier steht nur, wo es liegt und wie es heißt.
# Voraussetzungen:
#   - die Plugin-Repos als Geschwister von diesem Repo (pro Plugin über $<NAME>_REPO überschreibbar)
#   - der Callora-Checkout für die CLI ($CALLORA_REPO)
#   - die aktuellen CalloraVoipSdk-Pakete in ../voip/artifacts/local-feed ($CALLORA_VOIP_SDK_FEED)
#   - je Plugin ein ECDSA-P256-Schlüssel in .signing/<name>.pem; einmalig erzeugen mit
#       openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out .signing/<name>.pem
#
# Auswahl: ohne Argumente alle, sonst nur die genannten.
#   scripts/bundle-plugins.sh                    # alle
#   scripts/bundle-plugins.sh composer           # nur eines
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CALLORA="${CALLORA_REPO:-$REPO_ROOT/../callora}"
VOIP_SDK_FEED="${CALLORA_VOIP_SDK_FEED:-$REPO_ROOT/../voip/artifacts/local-feed}"
CLI_PROJ="$CALLORA/src/Host/Cli/Callora.Host.Cli.csproj"

# name | Repo-Verzeichnis | Projektdatei relativ zum Repo | Zielverzeichnis relativ zu custom/
#
# Die Frontend-Bundles werden NICHT hier aufgezählt: Jedes Repo bringt sie in seinem eigenen
# `npm run build` mit, und eine Liste hier wäre eine zweite Wahrheit, die beim nächsten
# hinzugefügten Bundle stillschweigend veraltet — genau so war die Block-Palette einmal leer.
PLUGINS=(
  "communication|callora-communication|Callora.Plugin.Communication.csproj|static-plugins/Communication"
  "composer|callora-composer|Callora.Plugin.Composer.csproj|static-plugins/Composer"
  "videoconference|callora-videoconference|src/VideoConference/VideoConference.csproj|plugins/videoconference"
)

selected=("$@")

is_selected() {
    [ ${#selected[@]} -eq 0 ] && return 0
    for want in "${selected[@]}"; do
        [ "$want" = "$1" ] && return 0
    done
    return 1
}

require() {
    if [ ! -e "$2" ]; then
        echo "FEHLER: $1 nicht gefunden: $2" >&2
        exit 1
    fi
}

require "Callora-CLI" "$CLI_PROJ"
require "CalloraVoipSdk-Feed" "$VOIP_SDK_FEED"

for entry in "${PLUGINS[@]}"; do
    IFS='|' read -r name repo_dir project dest_rel <<<"$entry"

    is_selected "$name" || continue

    upper=$(echo "$name" | tr '[:lower:]' '[:upper:]')
    repo_var="${upper}_REPO"
    repo="${!repo_var:-$REPO_ROOT/../$repo_dir}"
    key_var="${upper}_SIGNING_KEY"
    key="${!key_var:-$REPO_ROOT/.signing/$name.pem}"
    dest="$REPO_ROOT/custom/$dest_rel"

    require "Repository für $name" "$repo"
    require "Projekt für $name" "$repo/$project"
    require "Signierschlüssel für $name" "$key"

    echo
    echo "══ $name ══"

    # Frontend-Bundles: was das Repo unter `build` deklariert. Ohne sie liefert das Plugin
    # zwar aus, aber ohne Oberfläche — und niemand erfährt, warum.
    if [ -f "$repo/package.json" ] && grep -q '"build"' "$repo/package.json"; then
        echo "-> npm run build"
        (cd "$repo" && npm run build --silent)
    fi

    echo "-> publish"
    # Eigener NuGet-Cache je Lauf: Bei gleicher Versionsnummer gäbe der globale Cache die
    # ALTEN Paketinhalte zurück, und ein frisch gepacktes Callora käme nie an.
    cache=$(mktemp -d)
    rm -rf "$dest"
    mkdir -p "$dest"
    NUGET_PACKAGES="$cache" dotnet publish "$repo/$project" \
        -c Release \
        -o "$dest" \
        --disable-build-servers \
        -m:1 \
        --tl:off \
        -p:RestoreAdditionalProjectSources="$VOIP_SDK_FEED"
    rm -rf "$cache"

    echo "-> sign"
    dotnet run --project "$CALLORA/src/Host/Cli" -c Release -- \
        plugin sign --plugin "$dest" --key "$key"

    echo "-> gebündelt + signiert: $dest"
done

echo
echo "-> ÖFFENTLICHE SCHLÜSSEL — als BackendHost:TrustedSigners[].PublicKey in appsettings"
echo "   (nur DIESER macht die ECDSA-Verifikation möglich; als JSON-String mit \\n je Zeile):"
for entry in "${PLUGINS[@]}"; do
    IFS='|' read -r name _ _ _ <<<"$entry"
    is_selected "$name" || continue
    upper=$(echo "$name" | tr '[:lower:]' '[:upper:]')
    key_var="${upper}_SIGNING_KEY"
    key="${!key_var:-$REPO_ROOT/.signing/$name.pem}"
    echo "── $name"
    openssl pkey -in "$key" -pubout
done
