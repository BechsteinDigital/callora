#!/usr/bin/env bash
# Gleicht PublicAPI.Unshipped.txt mit dem tatsächlichen Stand ab.
#
# Die Grundlinie ist ein Vertrag: Jedes öffentliche Symbol steht darin, und ein Symbol
# darin, das es nicht mehr gibt, ist ein Fehler. Beides meldet der Analyzer als RS0016
# (fehlt) und RS0017 (überzählig) — mit dem vollständigen Symbolnamen in der Meldung.
# Genau den trägt dieses Skript ein bzw. aus.
#
# Es entscheidet nichts. Wer eine öffentliche Fläche ändert, hat die Entscheidung schon
# getroffen; hier wird sie nur aufgeschrieben, statt sie ein drittes Mal von Hand aus
# Compiler-Meldungen zu kopieren.
#
#   scripts/sync-public-api.sh                    # alle Projekte mit Grundlinie
#   scripts/sync-public-api.sh src/Core           # nur eines
set -euo pipefail

cd "$(dirname "$0")/.."

targets=("$@")
if [ ${#targets[@]} -eq 0 ]; then
    mapfile -t targets < <(find src custom -name PublicAPI.Unshipped.txt -not -path '*/bin/*' -not -path '*/obj/*' -printf '%h\n' | sort -u)
fi

for dir in "${targets[@]}"; do
    project=$(find "$dir" -maxdepth 1 -name '*.csproj' | head -1)
    if [ -z "$project" ]; then
        echo "übersprungen: $dir hat kein Projekt" >&2
        continue
    fi

    baseline="$dir/PublicAPI.Unshipped.txt"
    log=$(mktemp)
    # Der Build MUSS scheitern dürfen: RS0016/RS0017 sind als Fehler konfiguriert, und
    # genau diese Fehler sind die Eingabe. `|| true` statt Abbruch.
    dotnet build "$project" --nologo -v quiet >"$log" 2>&1 || true

    python3 - "$baseline" "$log" <<'PY'
import pathlib
import re
import sys

baseline_path, log_path = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
log = log_path.read_text(errors="replace")

# RS0016: fehlt in der Grundlinie · RS0017: steht darin, existiert aber nicht mehr.
pattern = r"error (RS001[67]): Symbol '(.+?)' is (?:not part of|part of)"
add, remove = set(), set()
for code, symbol in re.findall(pattern, log):
    (add if code == "RS0016" else remove).add(symbol)

if not add and not remove:
    print(f"{baseline_path.parent}: unverändert")
    sys.exit()

entries = {line for line in baseline_path.read_text().splitlines() if line.strip()}
header = [line for line in baseline_path.read_text().splitlines()[:1] if line.startswith("#")]
entries -= remove
entries |= add

baseline_path.write_text("\n".join(header + sorted(entries - set(header))) + "\n")
print(f"{baseline_path.parent}: +{len(add)} −{len(remove)}")
PY
    rm -f "$log"
done
