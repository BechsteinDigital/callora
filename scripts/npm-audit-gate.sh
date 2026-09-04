#!/usr/bin/env bash
#
# Das Audit-Gate für ausgelieferte Abhängigkeiten — mit Zeitlimit und getrennten Gründen.
#
# Der Befund, der das nötig machte: `npm audit --omit=dev --audit-level=high` wirft „Schwachstelle
# gefunden" und „konnte den Dienst nicht fragen" in denselben Exit-Code. Am 04.09.2026 waren beide
# Advisory-Endpunkte von npm gestört — `advisories/bulk` lief in eine Zeitüberschreitung,
# `audits/quick` antwortete mit 500 — und die CI meldete das als Fehlschlag zweier Jobs, nach fünf
# Minuten fünfzig, mit der Meldung „Invalid package tree, run npm install". Die war frei erfunden:
# `npm ci` war unmittelbar davor sauber durchgelaufen. Wer das liest, sucht am falschen Ort.
#
# Beide Fälle scheitern weiterhin. Ein Gate, das bei nicht erreichbarem Dienst still durchwinkt,
# ist schlimmer als keins — es sagt „geprüft", wo niemand geprüft hat. Was sich ändert: Es dauert
# eine Minute statt sechs, und die Meldung benennt, welcher der beiden Fälle vorliegt.

set -uo pipefail

readonly TIMEOUT_SECONDS="${NPM_AUDIT_TIMEOUT_SECONDS:-60}"

output="$(timeout "${TIMEOUT_SECONDS}" npm audit --omit=dev --audit-level=high 2>&1)"
status=$?

printf '%s\n' "${output}"

if [ "${status}" -eq 0 ]; then
  exit 0
fi

# 124 ist die Zeitüberschreitung von `timeout`; die Fehlermeldung deckt den Fall ab, in dem der
# Endpunkt schnell genug antwortet, um zu scheitern (400, 500, 503).
if [ "${status}" -eq 124 ] || printf '%s' "${output}" | grep -q 'audit endpoint returned an error'; then
  echo "::error title=npm-Audit nicht erreichbar::Der Advisory-Dienst von npm hat nicht geantwortet (Abbruch nach ${TIMEOUT_SECONDS}s oder Fehler des Endpunkts). Das ist KEIN Fund — es wurde nichts geprüft. Erneut laufen lassen, sobald npm wieder antwortet; hält es an, siehe https://status.npmjs.org/"
  exit 1
fi

echo "::error title=Verwundbare ausgelieferte Abhängigkeit::npm audit meldet mindestens einen Fund der Stufe high oder höher in den Produktionsabhängigkeiten. Behebung über ein Versions-Update — siehe die offenen Dependabot-Pull-Requests."
exit 1
