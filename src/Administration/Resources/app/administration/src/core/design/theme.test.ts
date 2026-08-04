import { beforeEach, describe, expect, it, vi } from 'vitest'
import { initTheme, resetTheme, THEME_STORAGE_KEY, useTheme } from './theme'

// Drives the system-preference signal the store listens to. Returns the change
// callbacks so a test can simulate the OS flipping between light and dark.
function stubMatchMedia(prefersDark: boolean): { flip: (dark: boolean) => void } {
  const listeners: Array<(e: MediaQueryListEvent) => void> = []
  let matches = prefersDark
  vi.stubGlobal('matchMedia', (query: string) => ({
    media: query,
    get matches() {
      return matches
    },
    addEventListener: (_: string, cb: (e: MediaQueryListEvent) => void) => listeners.push(cb),
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
    onchange: null,
  }))
  return {
    flip: (dark: boolean) => {
      matches = dark
      for (const cb of listeners) {
        cb({ matches: dark } as MediaQueryListEvent)
      }
    },
  }
}

describe('theme store', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
    resetTheme()
    stubMatchMedia(true)
  })

  it('defaults to following the system and sets no data-theme attribute', () => {
    initTheme()
    const { preference } = useTheme()

    expect(preference.value).toBe('system')
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false)
  })

  it('resolves the system preference to the actual colour scheme', () => {
    stubMatchMedia(false)
    initTheme()

    expect(useTheme().resolved.value).toBe('light')
  })

  it('follows the system when it changes while on the system preference', () => {
    const media = stubMatchMedia(false)
    initTheme()
    const { resolved } = useTheme()
    expect(resolved.value).toBe('light')

    media.flip(true)

    expect(resolved.value).toBe('dark')
  })

  it('pins the document to an explicit choice and persists it', () => {
    initTheme()
    const { setPreference, resolved } = useTheme()

    setPreference('light')

    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
    expect(resolved.value).toBe('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('ignores the system signal once a preference is pinned', () => {
    const media = stubMatchMedia(false)
    initTheme()
    const { setPreference, resolved } = useTheme()
    setPreference('dark')

    media.flip(false)

    expect(resolved.value).toBe('dark')
  })

  it('restores a persisted preference on boot', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light')

    initTheme()

    expect(useTheme().preference.value).toBe('light')
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')
  })

  it('falls back to the system preference when the stored value is unknown', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'sepia')

    initTheme()

    expect(useTheme().preference.value).toBe('system')
  })

  it('toggles between the two explicit modes starting from what is shown', () => {
    stubMatchMedia(true)
    initTheme()
    const { toggle, preference, resolved } = useTheme()

    toggle()

    expect(preference.value).toBe('light')
    expect(resolved.value).toBe('light')

    toggle()

    expect(preference.value).toBe('dark')
  })

  it('survives unavailable storage', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage disabled')
    })
    initTheme()

    expect(() => useTheme().setPreference('light')).not.toThrow()
    expect(document.documentElement.getAttribute('data-theme')).toBe('light')

    setItem.mockRestore()
  })
})
