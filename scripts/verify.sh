#!/usr/bin/env bash
#
# Fährt lokal die Gates, an denen die CI scheitern würde — in ihrer Reihenfolge.
#
# Warum es das gibt: Solange die CI wegen des Actions-Kontingents nicht läuft, prüft niemand
# fünf Dinge, die ein "dotnet test" nicht abdeckt. Beim ersten Lauf dieses Skripts waren zwei
# davon auf main bereits rot — die Landkarte (die Dateizähler waren um zwölf Dateien veraltet)
# und eine hoch eingestufte Lücke in einer Produktionsabhängigkeit des öffentlich ausgelieferten
# Surface-Bundles. Beides wäre unentdeckt geblieben, bis jemand das Kontingent wieder freigibt.
#
# Bewusst NICHT enthalten: Artefakt-Upload, Pages-Deploy und die Runner-Matrix. Das sind Dinge,
# die nur auf GitHub Sinn ergeben; hier zählt, was rot werden kann.
#
#   bash scripts/verify.sh            # alles
#   bash scripts/verify.sh --fast     # ohne Release-Build und ohne Coverage (~3 statt ~12 Minuten)
#
set -uo pipefail
cd "$(dirname "$0")/.."

FAST=0
[ "${1:-}" = "--fast" ] && FAST=1

FAILED=()
step() {
  local name="$1"; shift
  printf '\n\033[1m▸ %s\033[0m\n' "$name"
  if "$@"; then
    printf '  \033[32m✓ %s\033[0m\n' "$name"
  else
    printf '  \033[31m✗ %s\033[0m\n' "$name"
    FAILED+=("$name")
  fi
}

# --- Landkarte -------------------------------------------------------------------------------
# Das Gate, das am leisesten bricht: Wer eine Datei anlegt, verschiebt einen Zähler in
# REPO_MAP.md, und "git diff --exit-code" in der CI ist danach rot. Beim Arbeiten merkt das
# niemand, weil nichts davon abhängt.
map_current() {
  bash scripts/build-repo-map.sh >/dev/null 2>&1 || return 1
  git diff --exit-code --quiet docs/REPO_MAP.md || {
    echo "  REPO_MAP.md ist veraltet — die Änderung liegt jetzt im Arbeitsverzeichnis."
    return 1
  }
}

# --- .NET ------------------------------------------------------------------------------------
dotnet_build_debug() { dotnet build Callora.Host.sln -p:SkipAdminFrontend=true --nologo -v q; }
dotnet_build_release() { dotnet build Callora.Host.sln --configuration Release --nologo -v q; }
dotnet_tests() { dotnet test Callora.Host.sln -p:SkipAdminFrontend=true --nologo -v q; }

dotnet_coverage() {
  rm -rf ./test-results
  dotnet test Callora.Host.sln --no-build --configuration Release \
    --collect:"XPlat Code Coverage" --results-directory ./test-results >/dev/null 2>&1 || return 1
  python3 - <<'PY'
import glob, sys, xml.etree.ElementTree as ET
covered = valid = 0
for report in glob.glob("./test-results/**/coverage.cobertura.xml", recursive=True):
    root = ET.parse(report).getroot()
    covered += int(root.get("lines-covered")); valid += int(root.get("lines-valid"))
if not valid:
    sys.exit("  Kein Coverage-Bericht gefunden.")
rate = covered / valid
print(f"  {rate:.1%} über {valid} Zeilen (Schwelle 25%)")
sys.exit(0 if rate >= 0.25 else 1)
PY
}

# --- Frontends -------------------------------------------------------------------------------
# Die drei Suiten haben ABSICHTLICH unterschiedliche Gates (ci.yml, Matrix "frontends" plus der
# eigene admin-frontend-Job) — die Docs-Site hat gar kein test-Skript, und nur sie wird gelintet.
# Ein Skript, das überall dasselbe fährt, meldet Rot, wo keines ist, und genau das gewöhnt man
# sich ab. Deshalb hier je Suite die Schritte, die die CI wirklich ausführt.
#
# npm ci vorweg, weil "vitepress: Kommando nicht gefunden" sonst wie ein kaputter Build aussieht
# und keiner ist.
npm_install() { [ -d node_modules ] || npm ci >/dev/null 2>&1; }

admin_frontend() {
  ( cd src/Administration/Resources/app/administration || return 1
    npm_install || return 1
    npm audit --omit=dev --audit-level=high || return 1
    npm run test --silent >/dev/null 2>&1 || { echo "  Tests rot"; return 1; }
    npm run build --silent >/dev/null 2>&1 || { echo "  Build rot"; return 1; }
    npm run build:lib --silent >/dev/null 2>&1 || { echo "  build:lib rot"; return 1; }
    # Nach dem Bibliotheks-Build, weil beide gegen dist-lib prüfen: dass jeder Export auf eine
    # gebaute Datei zeigt, und dass die Scope-Ids der Bibliothek im Anwendungs-Build vorkommen.
    npx vitest run src/public/exports.test.ts src/public/scoped-styles.test.ts >/dev/null 2>&1 \
      || { echo "  Paketvertrag rot"; return 1; } )
}

surface_frontend() {
  ( cd src/Surface.Rendering/Resources/app/surface || return 1
    npm_install || return 1
    npm audit --omit=dev --audit-level=high || return 1
    npm run test --silent >/dev/null 2>&1 || { echo "  Tests rot"; return 1; }
    npm run build --silent >/dev/null 2>&1 || { echo "  Build rot"; return 1; } )
}

# Kein test-Skript, und die CI ruft dort auch keines auf (matrix.test: false).
docs_site() {
  ( cd docs-site || return 1
    npm_install || return 1
    npm audit --omit=dev --audit-level=high || return 1
    npm run lint --silent >/dev/null 2>&1 || { echo "  Lint rot"; return 1; }
    npm run build --silent >/dev/null 2>&1 || { echo "  Build rot"; return 1; } )
}

step "Landkarte aktuell"        map_current
step "Build (Debug, ohne SPA)"  dotnet_build_debug
step "Tests (.NET)"             dotnet_tests
step "Admin-Frontend"           admin_frontend
step "Surface-Frontend"         surface_frontend
step "Docs-Site"                docs_site

if [ "$FAST" = "0" ]; then
  step "Build (Release, mit SPA)" dotnet_build_release
  step "Coverage ≥ 25%"           dotnet_coverage
fi

printf '\n'
if [ ${#FAILED[@]} -eq 0 ]; then
  printf '\033[32mAlle Gates grün.\033[0m\n'
  exit 0
fi

printf '\033[31mRot: %s\033[0m\n' "${FAILED[*]}"
exit 1
