import { createI18n } from 'vue-i18n'
import { apiFetch } from '@/core/http'

/**
 * Die Oberflächentexte der Administration (#273, ADR-024).
 *
 * Dieselbe Quelle wie im Renderpfad: Die Nachrichten kommen vom Snippet-Endpunkt, nicht aus einer
 * Datei im Bundle. Sonst wäre jeder Text, den ein Betreiber im Admin ändert, im Admin selbst nicht
 * geändert — und ein System, dessen Verwaltung ihre eigene Zusage nicht einhält, erklärt sich
 * niemandem.
 *
 * Admin-Texte gelten im Geltungsbereich `global`: Die Verwaltung läuft ohne Workspace, und ein
 * Workspace-Filter an einer Stelle ohne Workspace wäre eine Bedingung, die nie zutrifft.
 */
const ADMIN_SCOPE = 'global'

export const i18n = createI18n({
  legacy: false,
  locale: 'de',
  fallbackLocale: 'de',
  // Leer, bis geladen wurde. Das ist kein Mangel, sondern der Normalzustand vor dem ersten
  // Rendern — `t` gibt dann den mitgegebenen Text zurück, siehe unten.
  messages: { de: {} },
  // Ein fehlender Schlüssel ist hier erwartbar (die Migration läuft schrittweise) und darf die
  // Konsole nicht fluten; was fehlt, zeigt das Gate in i18n.test.ts.
  missingWarn: false,
  fallbackWarn: false,
})

/**
 * Lädt die Texte einer Sprache in den Speicher der Anwendung.
 *
 * Ein Fehlschlag ist kein Grund, die Oberfläche nicht zu zeigen: Ohne Snippets steht überall der
 * eingebaute Text, und das ist genau der Zustand, aus dem die Migration kommt.
 */
export async function loadAdminSnippets(locale = 'de'): Promise<void> {
  try {
    const response = await apiFetch(`/api/snippets?locale=${encodeURIComponent(locale)}&scope=${ADMIN_SCOPE}`)
    if (!response.ok) {
      return
    }

    const entries = (await response.json()) as { snippetKey: string; effectiveValue: string }[]
    i18n.global.setLocaleMessage(
      locale,
      Object.fromEntries(entries.map((entry) => [entry.snippetKey, entry.effectiveValue])),
    )
  } catch {
    // Bewusst geschluckt: siehe oben. Ein Fehler beim Laden von Texten darf die Verwaltung nicht
    // unbenutzbar machen.
  }
}

/**
 * Der Übersetzungsaufruf der Oberfläche: Schlüssel plus der Text, der bisher dort stand.
 *
 * Der zweite Parameter ist der Grund, warum sich jede Datei einzeln umstellen lässt und keine
 * Zwischenstufe Schlüssel statt Text zeigt:
 *
 * ```vue
 * {{ t('admin.user.create', 'Benutzer anlegen') }}
 * ```
 */
export function t(key: string, fallback: string): string {
  return i18n.global.te(key) ? i18n.global.t(key) : fallback
}
