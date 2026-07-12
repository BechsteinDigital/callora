const FALLBACK_LOCALE = "en";

/**
 * Shared snippet registry — Twig's trans for shell extensions. Plugins
 * register locale-keyed snippet maps; consumers resolve by the active locale
 * with an English fallback.
 */
export function createShellSnippetRegistry(stateKey: string) {
  const snippetsByLocale = useState<Record<string, Record<string, string>>>(stateKey, () => ({}));
  const activeLocale = useState<string>(`${stateKey}-locale`, () => FALLBACK_LOCALE);

  function registerSnippets(locale: string, values: Record<string, string>): void {
    const normalizedLocale = locale?.trim().toLowerCase();
    if (!normalizedLocale || !values) {
      return;
    }

    const existing = snippetsByLocale.value[normalizedLocale] ?? {};
    snippetsByLocale.value = {
      ...snippetsByLocale.value,
      [normalizedLocale]: { ...existing, ...values }
    };
  }

  function translate(snippetKey: string, fallback?: string): string {
    const key = snippetKey?.trim();
    if (!key) {
      return fallback ?? "";
    }

    return snippetsByLocale.value[activeLocale.value]?.[key] ??
      snippetsByLocale.value[FALLBACK_LOCALE]?.[key] ??
      fallback ??
      key;
  }

  function setLocale(locale: string): void {
    const normalized = locale?.trim().toLowerCase();
    if (normalized) {
      activeLocale.value = normalized;
    }
  }

  return {
    activeLocale: readonly(activeLocale),
    registerSnippets,
    translate,
    setLocale
  };
}
