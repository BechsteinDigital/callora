#!/usr/bin/env bash
set -uo pipefail

# Die CI lokal — dieselben Befehle, ohne GitHub.
#
# Dies spiegelt .github/workflows/ci.yml und docs.yml Schritt für Schritt. Nicht der
# Actions-Runner wird nachgebaut (dafür gäbe es `act`), sondern das, was die Jobs
# tatsächlich ausführen. Der Unterschied ist praktisch: `act` lädt GB-große Images und
# scheitert an Caches, CodeQL und Service-Containern; die Befehle hier sind genau die,
# die im Zweifel rot werden.
#
# Wie die CI läuft jedes Gate zu Ende, auch wenn ein früheres fehlschlägt
# (fail-fast: false) — ein kaputtes Frontend soll den Zustand der anderen nicht
# verdecken. Der Exit-Code ist am Ende rot, wenn irgendein Gate rot war.
#
#   ./scripts/ci-local.sh                    # alles
#   ./scripts/ci-local.sh --only dotnet      # nur Build & Test
#   ./scripts/ci-local.sh --skip docs        # ohne die Doku-Site
#   ./scripts/ci-local.sh --no-audit         # ohne npm audit (offline)
#   ./scripts/ci-local.sh --list
#
# Nicht parallel zu anderer schwerer Arbeit laufen lassen: Der Surface-Renderer hat ein
# Wanduhr-Limit, und unter CPU-Konkurrenz kippen einzelne Render-Tests, ohne dass am Code
# etwas falsch ist. Die CI hat den Rechner für sich; hier muss man daran denken.
#
# Was hier NICHT läuft und nur auf GitHub existiert:
#   - CodeQL (C#/JS) — braucht die GitHub-Analyse-Infrastruktur
#   - communication-interop.yml — braucht einen echten Asterisk in Docker;
#     dafür `ops/spikes/asterisk-b4deep3/` von Hand starten
#   - der Pages-Deploy und der Release-Smoke gegen Postgres

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

ALL_GATES=(dotnet golden admin frontends docs)
RUN_AUDIT="true"
SELECTED=""
SKIPPED=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --only) SELECTED="${2:-}"; shift 2 ;;
    --skip) SKIPPED="${2:-}"; shift 2 ;;
    --no-audit) RUN_AUDIT="false"; shift ;;
    --list)
      printf '%s\n' "${ALL_GATES[@]}"
      exit 0
      ;;
    *)
      echo "Usage: $0 [--only <gates>] [--skip <gates>] [--no-audit] [--list]" >&2
      echo "Gates: ${ALL_GATES[*]} (kommasepariert)" >&2
      exit 1
      ;;
  esac
done

wanted() {
  local gate="$1"
  if [[ -n "$SELECTED" ]]; then
    [[ ",$SELECTED," == *",$gate,"* ]] || return 1
  fi
  if [[ -n "$SKIPPED" ]]; then
    [[ ",$SKIPPED," == *",$gate,"* ]] && return 1
  fi
  return 0
}

FAILED=()
PASSED=()

step() { printf '\n\033[1m▸ %s\033[0m\n' "$1"; }

record() {
  local name="$1" status="$2"
  if [[ "$status" -eq 0 ]]; then
    PASSED+=("$name")
  else
    FAILED+=("$name")
  fi
}

# Node 22 in der CI (setup-node). Eine andere Major-Version lokal heißt: ein grüner
# Lauf hier sagt nichts über den dort.
if command -v node > /dev/null; then
  NODE_MAJOR="$(node --version | sed 's/^v\([0-9]*\).*/\1/')"
  if [[ "$NODE_MAJOR" != "22" ]]; then
    echo "warnung: Node $NODE_MAJOR lokal, die CI benutzt 22 — Ergebnisse sind nicht vergleichbar."
  fi
fi

audit() {
  [[ "$RUN_AUDIT" == "true" ]] || return 0
  # Ausgelieferte Abhängigkeiten sind das Gate; alles inklusive dev ist beratend.
  npm audit --omit=dev --audit-level=high || return 1
  npm audit --audit-level=high || true
}

# npm-Pakete, die per file: aufeinander zeigen, verlinken das Quellverzeichnis —
# nicht ein gebautes Tarball. Ohne build:lib der Abhängigkeit scheitert der
# Konsument mit ERR_MODULE_NOT_FOUND.
build_lib() {
  ( cd "$1" && npm ci && npm run build:lib )
}

# ── dotnet ────────────────────────────────────────────────────────────────────
if wanted dotnet; then
  step "dotnet — Build & Test (ci.yml: build-test)"
  (
    set -e
    dotnet restore Callora.Host.sln
    dotnet build Callora.Host.sln --no-restore --configuration Release
    rm -rf ./test-results
    dotnet test Callora.Host.sln --no-build --configuration Release \
      --collect:"XPlat Code Coverage" --results-directory ./test-results
    # Über ALLE Berichte, nicht über den ersten — derselbe Fehler wie in ci.yml:
    # der Lauf erzeugt zwei, und der kleine (Analyzer-Tests) meldet 93,6 % statt 33,6 %.
    python3 - <<'PY'
import glob, sys, xml.etree.ElementTree as ET

threshold = 0.25
reports = glob.glob("./test-results/**/coverage.cobertura.xml", recursive=True)
if not reports:
    sys.exit("Kein Coverage-Report gefunden.")

covered = valid = 0
for report in reports:
    root = ET.parse(report).getroot()
    covered += int(root.get("lines-covered"))
    valid += int(root.get("lines-valid"))

rate = covered / valid if valid else 0.0
print(f"line coverage: {rate:.1%} über {valid} Zeilen (threshold {threshold:.0%})")
sys.exit(0 if rate >= threshold else 1)
PY
  )
  record dotnet $?
fi

# ── golden ────────────────────────────────────────────────────────────────────
if wanted golden; then
  step "golden — ein Plugin von außen bauen, prüfen und signieren (golden-path.yml)"
  ./scripts/golden-path.sh
  record golden $?
fi

# ── admin ─────────────────────────────────────────────────────────────────────
if wanted admin; then
  step "admin — Admin-Shell (ci.yml: admin-frontend)"
  (
    set -e
    cd src/Administration/Resources/app/administration
    npm ci
    audit
    # Der Extension-Point-Katalog wird aus der Shell generiert. Wanderte ein Slot,
    # ohne dass jemand neu generiert, verspräche @callora/admin einen Punkt, den es
    # nicht mehr gibt.
    npm run generate:catalog
    git diff --exit-code src/core/extensions/catalog.generated.ts src/core/extensions/catalog.json
    npm run test
    npm run build
    npm run build:lib
    # Nach build:lib, weil beide Tests gegen dist-lib prüfen.
    npx vitest run src/public/exports.test.ts src/public/scoped-styles.test.ts
  )
  record admin $?
fi

# ── frontends ─────────────────────────────────────────────────────────────────
if wanted frontends; then
  SURFACE="src/Surface.Rendering/Resources/app/surface"
  ADMIN="src/Administration/Resources/app/administration"

  step "frontends — Surface Runtime (test + build)"
  ( set -e; cd "$SURFACE"; npm ci; audit; npm run test; npm run build )
  record "frontends:surface" $?

  step "frontends — Communication Admin (build)"
  (
    set -e
    build_lib "$ADMIN"
    build_lib "$SURFACE"
    cd custom/static-plugins/Communication
    npm ci; audit; npm run build
  )
  record "frontends:communication" $?

  step "frontends — Composer Admin (test + build)"
  (
    set -e
    build_lib "$ADMIN"
    build_lib "$SURFACE"
    cd custom/static-plugins/Composer
    npm ci; audit; npm run test; npm run build
  )
  record "frontends:composer" $?
fi

# ── docs ──────────────────────────────────────────────────────────────────────
if wanted docs; then
  step "docs — VitePress + DocFX (docs.yml)"
  (
    set -e
    ( cd docs-site && npm ci && npm run lint && npm run build )
    dotnet tool restore
    dotnet docfx docfx/docfx.json
  )
  record docs $?
fi

# ── Ergebnis ──────────────────────────────────────────────────────────────────
echo
for gate in "${PASSED[@]:-}"; do [[ -n "$gate" ]] && printf '\033[32m  ok   %s\033[0m\n' "$gate"; done
for gate in "${FAILED[@]:-}"; do [[ -n "$gate" ]] && printf '\033[31m  FAIL %s\033[0m\n' "$gate"; done

if [[ ${#FAILED[@]} -gt 0 ]]; then
  echo
  echo "ci-local: ${#FAILED[@]} Gate(s) rot."
  exit 1
fi

echo
echo "ci-local: alle Gates grün."
