import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Spiegelt SnippetApiResponse (Core). Basis und Abweichung stehen getrennt, weil die Verwaltung
// genau diese Frage beantworten muss: Was hat der Betreiber geändert?
export interface Snippet {
  snippetKey: string
  locale: string
  /** Wer den Text mitgebracht hat; leer bei einer verwaisten Abweichung. */
  pluginId: string
  /** Was das Paket liefert; null, wenn es den Schlüssel nicht (mehr) kennt. */
  baseValue: string | null
  /** Was auf DIESER Ebene gesetzt ist — nicht, was von weiter oben durchschlägt. */
  overrideValue: string | null
  effectiveValue: string
  isOverridden: boolean
  /** Ein Override, dessen Schlüssel aus dem Paket verschwunden ist. */
  isOrphaned: boolean
}

export interface SnippetScope {
  locale: string
  scope: 'global' | 'tenant' | 'workspace'
  scopeKey: string
}

function query({ locale, scope, scopeKey }: SnippetScope): string {
  return `locale=${encodeURIComponent(locale)}&scope=${scope}&scopeKey=${encodeURIComponent(scopeKey)}`
}

export const snippetsApi = {
  async list(target: SnippetScope): Promise<Snippet[]> {
    const response = await unwrap(await apiFetch(`/api/snippets?${query(target)}`))
    return (await response.json()) as Snippet[]
  },

  async set(snippetKey: string, target: SnippetScope, value: string): Promise<void> {
    await unwrap(
      await apiFetch(
        `/api/snippets/${encodeURIComponent(snippetKey)}?${query(target)}`,
        jsonInit('PUT', { value }),
      ),
    )
  },

  /** Nimmt die Abweichung zurück — der Text des Pakets gilt danach wieder. */
  async reset(snippetKey: string, target: SnippetScope): Promise<void> {
    await unwrap(
      await apiFetch(`/api/snippets/${encodeURIComponent(snippetKey)}?${query(target)}`, {
        method: 'DELETE',
      }),
    )
  },
}
