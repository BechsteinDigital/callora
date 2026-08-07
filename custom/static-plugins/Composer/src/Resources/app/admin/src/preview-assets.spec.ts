import { describe, expect, it, vi } from 'vitest'
import { fetchSurfaceStyles, fetchTheme } from './preview-assets'

describe('fetchSurfaceStyles', () => {
  it('verkettet in Kettenreihenfolge, damit die spätere Regel gewinnt — wie auf der Fläche', async () => {
    const fetchText = vi.fn(async (url: string) => `/* ${url} */`)

    const css = await fetchSurfaceStyles(['/a.css', '/b.css'], fetchText)

    expect(css).toBe('/* /a.css */\n/* /b.css */')
  })

  it('lässt eine Datei ausfallen, ohne die anderen mitzunehmen', async () => {
    // Dieselbe Fehlertoleranz wie beim Laden der Bundles: Ein Plugin darf den Editor nicht
    // mitnehmen. Ungestylt ist sichtbar, leer wäre es nicht.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    const fetchText = vi.fn(async (url: string) => {
      if (url === '/kaputt.css') {
        throw new Error('404')
      }
      return `/* ${url} */`
    })

    const css = await fetchSurfaceStyles(['/kaputt.css', '/b.css'], fetchText)

    expect(css).toBe('/* /b.css */')
    expect(warn).toHaveBeenCalled()
    warn.mockRestore()
  })

  it('gibt bei leerer Liste einen leeren String zurück statt eines Zeilenumbruchs', async () => {
    expect(await fetchSurfaceStyles([], vi.fn())).toBe('')
  })
})

describe('fetchTheme', () => {
  it('fragt den öffentlichen Endpunkt der Fläche, nicht einen admin-eigenen', async () => {
    // Ein zweiter Weg zu denselben Werten wäre ein zweiter Weg, auf dem sie auseinanderlaufen.
    const fetchJson = vi.fn(async () => ({ valuesByKey: { 'color-primary': '#123456' } }))

    const theme = await fetchTheme('acme', fetchJson)

    expect(fetchJson).toHaveBeenCalledWith('/workspace/public/theme?workspaceKey=acme')
    expect(theme.valuesByKey).toEqual({ 'color-primary': '#123456' })
  })

  it('kodiert den Workspace-Schlüssel', async () => {
    const fetchJson = vi.fn(async () => ({}))

    await fetchTheme('a b/c', fetchJson)

    expect(fetchJson).toHaveBeenCalledWith('/workspace/public/theme?workspaceKey=a%20b%2Fc')
  })

  it('rendert mit den Standardwerten weiter, wenn das Theme nicht erreichbar ist', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    const theme = await fetchTheme('acme', vi.fn(async () => {
      throw new Error('offline')
    }))

    expect(theme).toEqual({ valuesByKey: {}, sectionLayouts: [] })
    warn.mockRestore()
  })
})
