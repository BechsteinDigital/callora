#!/usr/bin/env python3
"""Prüft die Zeilenabdeckung über ALLE Cobertura-Berichte eines Testlaufs.

    scripts/coverage-gate.py [--threshold 0.25] [--results ./test-results]

Über alle Berichte, nicht über den ersten: Ein Lauf erzeugt einen je Testprojekt, und
welchen das Dateisystem zuerst liefert, ist Zufall. Genau daran hing dieses Gate einmal —
es las den 282-Zeilen-Bericht der Analyzer-Tests und meldete 93,6 % Abdeckung, während der
84.000-Zeilen-Bericht des Kerns bei 33,6 % stand. Ein Gate, das die falsche Zahl prüft,
ist schlimmer als keines: Es beruhigt.

Lag vorher zweimal als eingebettetes Python vor, in ci.yml und in ci-local.sh. Zwei
Fassungen derselben Rechnung driften, und die eine, die lokal läuft, ist dann nicht mehr
die, die den Build rot macht.
"""

import argparse
import glob
import sys

# Der stdlib-Parser und nicht defusedxml: Gelesen wird ausschließlich, was `dotnet test`
# im selben Lauf unter --results-directory geschrieben hat. Es gibt keine fremde Eingabe,
# gegen die XXE oder eine Entity-Bombe gerichtet sein könnte, und defusedxml wäre eine
# pip-Abhängigkeit, die auf einem frischen Runner erst installiert werden müsste — für
# ein Skript, das sonst mit dem auskommt, was jedes System mitbringt.
import xml.etree.ElementTree as ET  # noqa: S314


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--threshold", type=float, default=0.25)
    parser.add_argument("--results", default="./test-results")
    args = parser.parse_args()

    reports = glob.glob(f"{args.results}/**/coverage.cobertura.xml", recursive=True)
    if not reports:
        print(f"Kein Coverage-Report unter {args.results}.", file=sys.stderr)
        return 1

    covered = valid = 0
    for report in sorted(reports):
        root = ET.parse(report).getroot()
        covered += int(root.get("lines-covered"))
        valid += int(root.get("lines-valid"))
        print(f"  {float(root.get('line-rate')):6.1%}  {root.get('lines-valid'):>7} Zeilen")

    rate = covered / valid if valid else 0.0
    print(f"Abdeckung: {rate:.1%} über {valid} Zeilen (Schwelle {args.threshold:.0%})")
    return 0 if rate >= args.threshold else 1


if __name__ == "__main__":
    sys.exit(main())
