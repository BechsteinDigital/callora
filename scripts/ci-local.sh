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

ALL_GATES=(dotnet integration golden admin frontends docs)
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
  step "dotnet — Landkarte, Build & Test (ci.yml: dotnet)"
  (
    set -e
    # Eine Sekunde, und sie hätte zwei rote Läufe erspart: ci.yml prüft die Karte als
    # ERSTEN Schritt, dieses Skript tat es überhaupt nicht — obwohl sein Kopf verspricht,
    # ci.yml Schritt für Schritt zu spiegeln.
    bash scripts/build-repo-map.sh
    git diff --exit-code docs/REPO_MAP.md

    dotnet restore Callora.Host.sln --verbosity quiet
    dotnet build Callora.Host.sln --no-restore --configuration Release --verbosity minimal
    rm -rf ./test-results
    # Ohne die Docker-Stufe, wie ci.yml sie trennt — die läuft im Gate `integration`.
    dotnet test Callora.Host.sln --no-build --configuration Release \
      --filter "Category!=Slow" \
      --logger "console;verbosity=minimal"
  )
  record dotnet $?
fi

# ── integration ───────────────────────────────────────────────────────────────
if wanted integration; then
  step "integration — Postgres-Stufe (ci.yml: integration)"
  # EIN Container für alle Klassen (PostgresFixture), isoliert über eine Datenbank je
  # Test. Ohne Docker überspringen sich die Tests selbst, statt rot zu werden.
  (
    set -e
    # ALLE Tests, nicht nur die Docker-Stufe: Die Abdeckung wird hier gemessen, und sie
    # wäre ohne die schnellen Tests genauso schief wie ohne die langsamen. Der dotnet-Job
    # bleibt die schnelle Rückmeldung; dieser hier ist der vollständige Lauf.
    rm -rf ./test-results
    dotnet test Callora.Host.sln --configuration Release \
      --collect:"XPlat Code Coverage" --results-directory ./test-results \
      --logger "console;verbosity=minimal" \
      -p:SkipAdminFrontend=true -p:SkipSurfaceFrontend=true
    python3 scripts/coverage-gate.py --threshold 0.25
  )
  record integration $?
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

  # Communication und Composer bauen in IHREN Repositories (ADR-020). Sie standen hier,
  # weil sie in einer Entwicklungsumgebung unter custom/ liegen — und machten diesen Lauf
  # für jeden lokal rot, dessen Klone einen anderen Stand haben. Ein Gate, das den Zustand
  # einer fremden Arbeitskopie prüft, wird ignoriert statt erfüllt. ci.yml kennt sie
  # ebenfalls nicht: custom/ ist gitignoriert und auf einem Runner leer.
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
