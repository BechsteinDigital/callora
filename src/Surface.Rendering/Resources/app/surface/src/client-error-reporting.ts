/**
 * Meldet Fehler aus dem Browser eines Besuchers an die Senke des Hosts (#294).
 *
 * Dieselbe Aufgabe wie im Admin und dieselben drei Regeln — Melden löst kein Melden aus, Gleiches
 * nur einmal, eine Obergrenze pro Seitenleben. Bewusst eine eigene Datei statt einer geteilten:
 * Admin und Fläche sind eigene Pakete, und sie aneinanderzukoppeln wäre der höhere Preis als
 * vierzig doppelte Zeilen.
 *
 * Der Unterschied liegt darin, wer hier sendet: eine öffentliche Seite ohne angemeldeten
 * Besucher. Deshalb gehen von hier nur Meldung, Stack und **Pfad** — die Query bleibt draußen,
 * bevor sie das Gerät verlässt. Der Server entschärft ein zweites Mal
 * (<c>ClientErrorSanitizer</c>), weil er sich auf keinen Absender verlassen kann; dass er es tut,
 * ist kein Grund, hier nachlässig zu sein.
 */
const ENDPOINT = '/api/client-errors'
const SOURCE = 'surface'
const MAX_REPORTS_PER_PAGE = 10

let sent = 0
const seen = new Set<string>()

export function reportClientError(error: unknown): void {
  const message = error instanceof Error ? error.message : String(error)
  const stack = error instanceof Error ? error.stack : undefined
  const fingerprint = `${message} ${stack ?? ''}`

  if (sent >= MAX_REPORTS_PER_PAGE || seen.has(fingerprint)) {
    return
  }

  seen.add(fingerprint)
  sent += 1

  void fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    // Nur der Pfad: Was in der Query einer Kundenseite steht, ist deren Sache und hat im
    // Betriebslog nichts verloren.
    body: JSON.stringify({ source: SOURCE, message, stack, url: window.location.pathname }),
    keepalive: true,
  }).catch(() => undefined)
}

export function installClientErrorReporting(): void {
  window.addEventListener('error', (event) => reportClientError(event.error ?? event.message))
  window.addEventListener('unhandledrejection', (event) => reportClientError(event.reason))
}

/** Für Tests: setzt Zähler und Gedächtnis zurück. */
export function resetClientErrorReporting(): void {
  sent = 0
  seen.clear()
}
