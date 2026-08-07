#!/usr/bin/env bash
set -euo pipefail

# Der Golden Path: die Kette, die ein fremder Plugin-Autor durchläuft — von außen.
#
#   packen → dotnet tool install → plugin new → publish → test-contract → sign
#
# Warum ein eigener Lauf, obwohl die Suite grün ist: Zwischen "im Repository baut es"
# und "gegen die veröffentlichten Pakete baut es" liegt eine Grenze, die kein Test in
# diesem Repository überquert. Beim ersten Durchlauf lagen dahinter vier Fehler, die im
# Repository sämtlich unsichtbar waren — eine Sicherheitsabsicherung, die nicht in die
# nuspec kam; ein SDK, das seine Analyzer nicht weitergab; ein Publish-Filter, der nur
# beim Build griff; und einer, der das Plugin selbst löschte. Keiner davon hätte je eine
# Suite rot gemacht.
#
# Der Lauf ist hermetisch: NUGET_PACKAGES zeigt in ein Arbeitsverzeichnis. Das ist nicht
# Kosmetik — der globale Cache gibt bei gleicher Versionsnummer die ALTEN Paketinhalte
# zurück, und ein Golden Path, der eine vorige Version prüft, prüft nichts.
#
#   ./scripts/golden-path.sh
#   ./scripts/golden-path.sh --work /tmp/gp --keep    # Arbeitsstand behalten

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK=""
KEEP="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --work) WORK="${2:-}"; shift 2 ;;
    --keep) KEEP="true"; shift ;;
    *) echo "Usage: $0 [--work <dir>] [--keep]" >&2; exit 1 ;;
  esac
done

if [[ -z "$WORK" ]]; then
  WORK="$(mktemp -d -t callora-golden-XXXXXX)"
fi
mkdir -p "$WORK"

if [[ "$KEEP" != "true" ]]; then
  trap 'rm -rf "$WORK"' EXIT
fi

FEED="$WORK/feed"
PLUGIN="$WORK/plugin"
TOOLS="$WORK/tools"
export NUGET_PACKAGES="$WORK/nuget-cache"

FAILED=0
step()  { printf '\n\033[1m▸ %s\033[0m\n' "$1"; }
ok()    { printf '\033[32m   ok   %s\033[0m\n' "$1"; }
fail()  { printf '\033[31m   FAIL %s\033[0m\n' "$1"; FAILED=1; }

# ── 1. Die Pakete bauen, gegen die ein Plugin kompiliert ──────────────────────
step "Pakete bauen"
cd "$ROOT_DIR"
dotnet pack Callora.Host.sln -p:SkipAdminFrontend=true --configuration Release \
  --output "$FEED" --nologo > "$WORK/pack.log" 2>&1 || {
    tail -30 "$WORK/pack.log"; fail "dotnet pack"; exit 1; }

for pkg in Callora.Cli Callora.Plugin.Sdk Callora.Core Callora.Analyzers; do
  if compgen -G "$FEED/$pkg.*.nupkg" > /dev/null; then ok "$pkg"; else fail "$pkg fehlt"; fi
done

# NuGet lädt einen Analyzer allein am Pfad. Eine Ebene tiefer — was etwa
# BuildOutputTargetFolder verursacht — installiert sich klaglos und läuft nie.
if unzip -l "$FEED"/Callora.Analyzers.*.nupkg | grep -q 'analyzers/dotnet/cs/Callora\.Analyzers\.dll'; then
  ok "Analyzer liegt unter analyzers/dotnet/cs"
else
  fail "Analyzer nicht am ladbaren Pfad"
fi

VERSION="$(ls "$FEED"/Callora.Cli.*.nupkg | sed 's/.*Callora\.Cli\.\(.*\)\.nupkg/\1/')"
echo "   Version: $VERSION"

# ── 2. Die CLI als Werkzeug installieren ─────────────────────────────────────
step "CLI als dotnet-Tool installieren"
dotnet tool install --tool-path "$TOOLS" --add-source "$FEED" \
  Callora.Cli --version "$VERSION" > "$WORK/tool.log" 2>&1 || {
    tail -20 "$WORK/tool.log"; fail "dotnet tool install"; exit 1; }
CALLORA="$TOOLS/callora"
"$CALLORA" --help > /dev/null && ok "callora --help"

# ── 3. Ein Plugin anlegen — außerhalb des Repositories ───────────────────────
step "Plugin scaffolden"
cd "$WORK"
"$CALLORA" plugin new GoldenPath --id golden-path --output "$PLUGIN" > /dev/null
[[ -f "$PLUGIN/registry.json" ]] && ok "registry.json" || fail "registry.json fehlt"

# Der Scaffolder muss das SDK referenzieren, nicht Callora.Core von Hand: Genau die
# handgeschriebene ExcludeAssets-Zeile ist die Falle, die das SDK beseitigt.
if grep -q 'Include="Callora.Plugin.Sdk"' "$PLUGIN"/*.csproj; then
  ok "referenziert Callora.Plugin.Sdk"
else
  fail "referenziert nicht das SDK"
  grep PackageReference "$PLUGIN"/*.csproj || true
fi

cat > "$PLUGIN/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="golden-path" value="$FEED" />
  </packageSources>
</configuration>
XML

# ── 4. Ausliefern und die Ausgabe prüfen ─────────────────────────────────────
step "publish — und was dabei herauskommt"
cd "$PLUGIN"
dotnet publish -c Release -o out --nologo > "$WORK/publish.log" 2>&1 || {
    tail -30 "$WORK/publish.log"; fail "dotnet publish"; exit 1; }

ASSEMBLY="$(ls out/*.dll 2>/dev/null | head -1 || true)"
[[ -n "$ASSEMBLY" ]] && ok "eigene Assembly: $(basename "$ASSEMBLY")" \
                     || fail "keine eigene Assembly im Publish-Ordner"

# Plattform-Assemblies neben dem Plugin brechen die Typidentität: Der Ladekontext
# leitet Callora.* an den Default-Kontext, eine zweite Kopie erzeugt denselben Typ
# doppelt — sichtbar erst beim Laden.
# Ausgenommen ist die eigene Assembly des Plugins, und zwar namentlich: Sie heißt
# Callora.Plugins.GoldenPath und passt damit auf jedes Muster, das man für
# "Plattform-Assembly" hinschreiben möchte.
OWN="$(basename "$ASSEMBLY" .dll)"
PLATFORM="$(ls out/ | grep -E '^Callora' | grep -v "^${OWN}\." || true)"
[[ -z "$PLATFORM" ]] && ok "keine Plattform-Assembly im Publish-Ordner" \
                     || { fail "Plattform-Assemblies ausgeliefert:"; echo "$PLATFORM"; }

# Schärfer als die vorige Prüfung, und aus einem konkreten Anlass: Fällt
# ExcludeAssets="runtime" von der SDK-Paketkante weg, fängt das MSBuild-Netz weiterhin
# Callora.Core ab — die obige Prüfung bliebe also grün, während der Plugin-Ordner still
# um EF Core, ASP.NET, Npgsql und OpenTelemetry wächst, rund 50 Dateien.
# Das scaffoldete Plugin hat keine eigenen Abhängigkeiten. Sein Publish-Ordner darf
# deshalb NICHTS enthalten außer seinen eigenen Dateien und dem Manifest; alles andere
# kam von der Plattform und hat dort nichts verloren.
UNERWARTET="$(ls out/ | grep -vE "^${OWN}\.|^registry\.json$" || true)"
[[ -z "$UNERWARTET" ]] && ok "Publish-Ordner enthält nur das Plugin und sein Manifest" \
                       || { fail "$(echo "$UNERWARTET" | wc -l) unerwartete Dateien im Publish-Ordner:"
                            echo "$UNERWARTET" | head -8; }

ADVISORIES="$(grep -c 'NU1903' "$WORK/publish.log" || true)"
[[ "$ADVISORIES" -eq 0 ]] && ok "keine Sicherheits-Advisory (NU1903)" \
                          || fail "$ADVISORIES NU1903-Meldungen — das Paket vererbt eine Lücke"

# ── 5. Gegen den Plattform-Vertrag prüfen ────────────────────────────────────
step "plugin test-contract"
BUILT="$(ls bin/Release/net10.0/*.dll | head -1)"
if "$CALLORA" plugin test-contract --assembly "$BUILT" --registry registry.json > "$WORK/contract.log" 2>&1; then
  ok "Vertragsprüfung bestanden"
else
  fail "Vertragsprüfung fehlgeschlagen"; tail -20 "$WORK/contract.log"
fi

# ── 6. Signieren — ohne Signatur kommt nichts in eine Distribution ───────────
step "plugin sign"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 \
  -out "$WORK/signing-key.pem" 2> /dev/null
# Signiert wird der PUBLISH-Ordner, nicht das Quellverzeichnis: Das Signatur-Manifest
# deckt die ausgelieferten Dateien ab, und ausgeliefert wird, was publish erzeugt —
# Assembly plus registry.json. Im Quellverzeichnis liegt die Assembly gar nicht.
if "$CALLORA" plugin sign --plugin "$PLUGIN/out" --key "$WORK/signing-key.pem" \
     --out "$WORK/plugin.signature.json" > "$WORK/sign.log" 2>&1; then
  [[ -s "$WORK/plugin.signature.json" ]] && ok "Signatur-Manifest erzeugt" \
                                         || fail "Signatur-Manifest ist leer"
else
  fail "Signieren fehlgeschlagen"; tail -20 "$WORK/sign.log"
fi

# ── 7. Gegenprobe: greift der Analyzer im fremden Repository? ────────────────
step "Gegenprobe — CAL0001 muss zubeißen"
cat >> src/GoldenpathPlugin.cs <<'CS'

internal sealed class GoldenPathAnalyzerProbe
{
    public Callora.Core.Application.Audit.IHostAuditStore? Verboten { get; set; }
}
CS
# Erst in eine Datei, dann greppen. Direkt zu pipen wäre falsch: Der Build SOLL hier
# fehlschlagen, und unter `set -o pipefail` gilt damit die ganze Pipeline als
# gescheitert — auch wenn grep den Treffer hatte. Die Gegenprobe hätte dann immer
# "still" gemeldet und genau das nicht geprüft, wofür sie da ist.
dotnet build -c Release --nologo > "$WORK/probe.log" 2>&1 || true
if grep -q 'CAL0001' "$WORK/probe.log"; then
  ok "CAL0001 bricht den Build"
else
  fail "CAL0001 blieb still — der Analyzer erreicht das Plugin nicht"
  tail -10 "$WORK/probe.log"
fi

# ── Ergebnis ─────────────────────────────────────────────────────────────────
echo
if [[ "$FAILED" -eq 0 ]]; then
  echo "golden-path: die Kette trägt."
else
  echo "golden-path: mindestens ein Schritt ist rot." >&2
fi
[[ "$KEEP" == "true" ]] && echo "Arbeitsstand: $WORK"
exit "$FAILED"
