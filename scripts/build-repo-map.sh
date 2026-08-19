#!/usr/bin/env bash
set -euo pipefail

# Erzeugt docs/REPO_MAP.md — die Landkarte des Repositories auf VERZEICHNIS-Ebene.
#
# Warum keine Dateiliste: Die erste Fassung dieses Skripts listete jede Datei
# einzeln und erzeugte 7.755 Zeilen, in denen `registry.json` die Beschreibung
# "JSON configuration/data" trug. Das ist ein Inventar, keine Karte — für einen
# Menschen unlesbar und für einen Agenten reines Kontextgift. Wer wissen will,
# welche Dateien es gibt, benutzt `find`; wer eine Karte braucht, will wissen,
# WOFÜR ein Verzeichnis da ist. Das kann keine Heuristik beantworten, deshalb
# steht die Bedeutung unten kuratiert und nur Struktur und Größe kommen aus dem
# Repository.
#
# Zwei Eigenschaften machen die Karte gate-fähig (siehe ci.yml):
#   1. Kein Zeitstempel in der Ausgabe. Sonst änderte sich die Datei bei jedem
#      Lauf und `git diff --exit-code` wäre immer rot.
#   2. Ein Verzeichnis ohne Eintrag in PURPOSES erscheint als "(nicht
#      beschrieben)" — das Gate zwingt damit jeden, der ein neues Modul anlegt,
#      es auch zu erklären. Genau das ist der Zweck: Die Karte kann nicht
#      veralten, ohne dass der Build es merkt.
#
# Aufruf:
#   ./scripts/build-repo-map.sh
#   ./scripts/build-repo-map.sh --out /tmp/map.md

OUT_PATH="docs/REPO_MAP.md"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --out) OUT_PATH="${2:-}"; shift 2 ;;
    *) echo "Usage: $0 [--out <path>]" >&2; exit 1 ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"
mkdir -p "$(dirname "$OUT_PATH")"

# Die kuratierte Hälfte: Pfad -> Zweck. Reihenfolge = Reihenfolge in der Karte.
# Ein Pfad, den es nicht mehr gibt, lässt das Skript scheitern (unten geprüft) —
# so kann diese Tabelle nicht auf gelöschte Verzeichnisse zeigen, wie es die
# Vorgängerfassung mit src/Client, src/Hosting und src/Modules tat.
PATHS=(
  "src/Core/Domain"
  "src/Core/Application"
  "src/Core/Infrastructure"
  "src/Core/Api"
  "src/Core/Extensibility"
  "src/Administration/Api"
  "src/Administration/Resources/app/administration"
  "src/Workspace"
  "src/Surface.Rendering"
  "src/Surface.Rendering/Resources/app/surface"
  "src/Surface.Rendering/Resources/views"
  "src/Analyzers"
  "src/Plugin.Sdk"
  "src/Host/Cli"
  "src/Host/Dev"
  "custom/plugins"
  "custom/static-plugins"
  "tests/Callora.Core.Tests"
  "tests/Callora.Analyzers.Tests"
  "tests/TestPlugins"
  "docs/adr"
  "docs-site"
  "ops"
  "scripts"
  ".github/workflows"
)

purpose_for() {
  case "$1" in
    "src/Core/Domain")            echo "Entitäten und Domänenregeln. Hängt von nichts ab — kein EF, kein ASP.NET." ;;
    "src/Core/Application")       echo "Use-Cases und Ports (Interfaces). Das Herz: Plugin-Laufzeit, Surfaces, Jobs, Flows, Sicherheit." ;;
    "src/Core/Infrastructure")    echo "Adapter auf konkrete Technik: EF/Postgres, Data Protection, HTTP, MCP, Startup-Dienste." ;;
    "src/Core/Api")               echo "Anmeldung und Token — der einzige anonyme Endpunkt-Satz des Kerns." ;;
    "src/Core/Extensibility")     echo "Die Marker-Attribute, auf denen CAL0001–0004 arbeiten. Klein und folgenreich." ;;
    "src/Administration/Api")     echo "Operator-API (/api/*): Plugins, Nutzer, Rollen, Workspaces, Surfaces, Themes." ;;
    "src/Administration/Resources/app/administration")
                                  echo "Admin-SPA (Vue 3, colocated) UND das npm-Paket @callora/admin, gegen das Plugins bauen." ;;
    "src/Workspace")              echo "Öffentliche Workspace-Routen und Theme-Auslieferung." ;;
    "src/Surface.Rendering")      echo "Server-Rendering der Flächen: Nunjucks auf Jint in einer gehärteten Sandbox." ;;
    "src/Surface.Rendering/Resources/app/surface")
                                  echo "Surface-Laufzeit im Browser und das npm-Paket @callora/surface." ;;
    "src/Surface.Rendering/Resources/views")
                                  echo "Die mitgelieferten Nunjucks-Templates (base, layout, section, page)." ;;
    "src/Analyzers")              echo "Roslyn-Analyzer CAL0001–0004. Bewachen die Vertragsgrenze zur Bauzeit." ;;
    "src/Plugin.Sdk")             echo "Paket ohne Code: Vertragsfläche + Analyzer + Build-Regeln in einer Referenz." ;;
    "src/Host/Cli")               echo "Die callora-CLI: plugin new, test-contract, sign." ;;
    "src/Host/Dev")               echo "Die einzige lauffähige Zusammenstellung im Repo. Kein Produkt — das liegt in callora-production." ;;
    "custom/plugins")             echo "Installationsziel für dynamische Plugins. Im Repository absichtlich leer." ;;
    "custom/static-plugins")      echo "Leer. Communication und Composer sind in eigene Repositories ausgezogen (ADR-020)." ;;
    "tests/Callora.Core.Tests")   echo "Die Hauptsuite. Enthält auch die Architektur- und Dokumentations-Gates." ;;
    "tests/Callora.Analyzers.Tests") echo "Analyzer-Tests: prüfen, dass CAL0001–0004 zubeißen und wo sie es nicht dürfen." ;;
    "tests/TestPlugins")          echo "Minimal-Plugins, gegen die die Laufzeit getestet wird (Export, eigener DbContext)." ;;
    "docs/adr")                   echo "Architekturentscheidungen. Bei Konflikt mit einem Issue führt das Issue." ;;
    "docs-site")                  echo "Die konzeptuelle Dokumentation (VitePress, Diátaxis)." ;;
    "ops")                        echo "Betrieb: Runbooks, Frontdoor-Konfiguration, npm-Ausnahmen." ;;
    "scripts")                    echo "Build-, Prüf- und Release-Automatisierung." ;;
    ".github/workflows")          echo "CI, Golden Path, Docs, Release, npm-Publish." ;;
    *)                            echo "(nicht beschrieben — bitte in scripts/build-repo-map.sh ergänzen)" ;;
  esac
}

# Gezählt wird, was IM REPOSITORY liegt, nicht was auf der Platte liegt.
#
# Vorher lief hier ein `find` mit einer Ausschlussliste für node_modules, bin, obj und
# dist. Die traf nicht, was sie treffen musste: `custom/plugins` und
# `custom/static-plugins` sind gitignoriert und in einer Entwicklungsumgebung voller
# geklonter Plugin-Repositories. Die Karte meldete dort 594 und 966 Dateien neben ihren
# eigenen Beschreibungen „Im Repository absichtlich leer" und „Leer" — eine Zeile, die
# sich selbst widerspricht.
#
# Schlimmer ist die Folge fürs Gate. `ci.yml` erzeugt die Karte neu und prüft sie mit
# `git diff --exit-code`. Auf einem Runner ohne die Klone kommen 2 und 2 heraus, auf
# jeder Maschine mit dem Dev-Stack etwas anderes — das Gate war also für jeden lokal
# rot, aus einem Grund, der mit seiner Arbeit nichts zu tun hat. Solche Gates werden
# gelöscht statt erfüllt.
#
# `git ls-files` beantwortet beide Punkte auf einmal und braucht keine Ausschlussliste,
# die beim nächsten Werkzeug wieder nachgezogen werden müsste: Was ignoriert ist, zählt
# nicht, und zwar überall gleich.
count_files() {
  git ls-files -- "$1" | wc -l | tr -d ' '
}

missing=()
for path in "${PATHS[@]}"; do
  [[ -e "$path" ]] || missing+=("$path")
done
if (( ${#missing[@]} > 0 )); then
  echo "FEHLER: Die Karte nennt Pfade, die es nicht (mehr) gibt:" >&2
  printf '  %s\n' "${missing[@]}" >&2
  echo "Eintrag in scripts/build-repo-map.sh streichen oder Pfad korrigieren." >&2
  exit 1
fi

# Verzeichnisse der ersten Ebene, die von KEINEM Karteneintrag abgedeckt sind.
uncovered=()
while IFS= read -r dir; do
  covered="false"
  for path in "${PATHS[@]}"; do
    [[ "$path" == "$dir"* ]] && { covered="true"; break; }
  done
  [[ "$covered" == "true" ]] || uncovered+=("$dir")
done < <(find . -maxdepth 1 -mindepth 1 -type d \
  -not -name '.git' -not -name 'node_modules' -not -name 'graphify-out' \
  -not -name '.config' -not -name 'docfx' -not -name 'bin' -not -name 'obj' \
  -not -name '.claude' \
  | sed 's|^\./||' | sort)

{
  echo "# Repository Map"
  echo
  echo "Erzeugt von \`scripts/build-repo-map.sh\`; CI prüft, dass sie aktuell ist."
  echo "Bedeutung ist kuratiert; Struktur und Größe kommen aus dem, was Git verfolgt —"
  echo "nicht aus dem Dateisystem, damit die Karte auf jeder Maschine dieselbe ist."
  echo
  echo "| Pfad | Dateien | Wofür |"
  echo "|---|---:|---|"
  for path in "${PATHS[@]}"; do
    echo "| \`${path}\` | $(count_files "$path") | $(purpose_for "$path") |"
  done
  if (( ${#uncovered[@]} > 0 )); then
    echo
    echo "## Nicht auf der Karte"
    echo
    echo "Diese Verzeichnisse hat noch niemand beschrieben:"
    echo
    for dir in "${uncovered[@]}"; do
      echo "- \`${dir}/\`"
    done
  fi
} > "$OUT_PATH"

echo "Karte geschrieben: $OUT_PATH ($(wc -l < "$OUT_PATH" | tr -d ' ') Zeilen)"
