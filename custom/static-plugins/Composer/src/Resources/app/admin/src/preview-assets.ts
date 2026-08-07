/**
 * Holt, was der Canvas braucht, um auszusehen wie die echte Fläche: die Stylesheets der
 * Plugins und die Theme-Tokens des Workspace.
 *
 * Beides wird als Text bzw. als Werte geholt statt eingebunden, weil beides gescoped
 * werden muss (siehe `scoped-surface-styles.ts`). Ein `<link>` lässt sich nicht scopen,
 * ohne seinen Inhalt zu kennen.
 */

/** Holt eine Datei als Text. Der Seam, den die Tests ersetzen. */
export type FetchText = (url: string) => Promise<string>

/** Holt JSON. Derselbe Seam für den Theme-Endpunkt. */
export type FetchJson = (url: string) => Promise<unknown>

/**
 * Verkettet die Stylesheets in Reihenfolge der Kette — dieselbe Reihenfolge, in der der
 * Browser sie auf der Fläche anwenden würde, also entscheidet auch hier die spätere Regel.
 *
 * Eine Datei, die nicht lädt, fällt aus; die anderen bleiben. Dieselbe Fehlertoleranz wie
 * beim Laden der Bundles: Ein Plugin darf den Editor nicht mitnehmen. Der Canvas sieht
 * dann für dieses eine Plugin ungestylt aus, was sichtbar ist — leer wäre es nicht.
 */
export async function fetchSurfaceStyles(
  urls: readonly string[],
  fetchText: FetchText = defaultFetchText,
): Promise<string> {
  const sheets = await Promise.all(
    urls.map(async (url) => {
      try {
        return await fetchText(url)
      } catch (error) {
        console.warn(`[composer] Stylesheet "${url}" konnte nicht geladen werden.`, error)
        return ''
      }
    }),
  )

  return sheets.filter((sheet) => sheet.length > 0).join('\n')
}

/**
 * Die Theme-Werte des Workspace, so wie der Server sie einer Fläche gibt.
 *
 * Der Endpunkt ist derselbe öffentliche, den die Fläche selbst abfragt — nicht ein
 * admin-eigener. Ein zweiter Weg zu denselben Werten wäre ein zweiter Weg, auf dem sie
 * auseinanderlaufen können, und genau das soll der Canvas nicht.
 */
export async function fetchThemeTokens(
  workspaceKey: string,
  fetchJson: FetchJson = defaultFetchJson,
): Promise<Record<string, string>> {
  try {
    const theme = (await fetchJson(
      `/workspace/public/theme?workspaceKey=${encodeURIComponent(workspaceKey)}`,
    )) as { valuesByKey?: Record<string, string> } | null

    return theme?.valuesByKey ?? {}
  } catch (error) {
    // Ohne Tokens rendert der Canvas mit den Standardwerten der Fläche. Das ist eine
    // schlechtere Vorschau, aber eine, die funktioniert.
    console.warn('[composer] Theme-Werte konnten nicht geladen werden.', error)
    return {}
  }
}

async function defaultFetchText(url: string): Promise<string> {
  const response = await fetch(url, { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`Fetch failed (${response.status}) for ${url}`)
  }

  return response.text()
}

async function defaultFetchJson(url: string): Promise<unknown> {
  const response = await fetch(url, {
    credentials: 'same-origin',
    headers: { accept: 'application/json' },
  })
  if (!response.ok) {
    throw new Error(`Fetch failed (${response.status}) for ${url}`)
  }

  return response.json()
}
