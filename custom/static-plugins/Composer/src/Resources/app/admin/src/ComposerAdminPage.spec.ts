import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import ComposerAdminPage from './ComposerAdminPage.vue'

/**
 * Zwei Aussagen, die beide still schiefgingen, wenn sie brächen:
 *
 * - Die Bundles werden für die Fläche DES LAYOUTS geladen. Für die falsche geladen, böte der
 *   Editor Blöcke an, die nach dem Veröffentlichen nicht da sind.
 * - Die Stylesheets werden NICHT eingebunden. Eingebunden, gestalteten sie die Admin-Shell um
 *   den Canvas herum um — genau das, wogegen das Scoping gebaut wurde.
 */
const loadSurfaceBundles = vi.fn()
vi.mock('@callora/surface', () => ({
  loadSurfaceBundles: (...args: unknown[]) => loadSurfaceBundles(...args),
}))

const fetchSurfaceStyles = vi.fn(async () => '.cal-header { color: red }')
const fetchThemeTokens = vi.fn(async () => ({ 'color-primary': '#123456' }))
vi.mock('./preview-assets', () => ({
  fetchSurfaceStyles: (...args: unknown[]) => fetchSurfaceStyles(...args),
  fetchThemeTokens: (...args: unknown[]) => fetchThemeTokens(...args),
}))

function draftResponse(surfaceKey: string | null) {
  return {
    ok: true,
    json: async () => ({
      layoutKey: 'portal',
      workspaceKey: 'acme',
      surfaceKey,
      versionNumber: 1,
      document: { sections: [] },
      changedAtUtc: '2026-08-07T00:00:00Z',
    }),
  }
}

beforeEach(() => {
  loadSurfaceBundles.mockReset()
  loadSurfaceBundles.mockResolvedValue({
    registry: {},
    results: [],
    styles: ['/plugin-assets/voip/app/workspace/main.css'],
  })
})

async function loadLayout(surfaceKey: string | null) {
  const wrapper = mount(ComposerAdminPage)
  await wrapper.find('input').setValue('portal')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  return wrapper
}

describe('ComposerAdminPage', () => {
  it('lädt die Bundles der Fläche, für die das Layout gedacht ist — und bindet nichts ein', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => draftResponse('kiosk')))

    await loadLayout('kiosk')

    expect(loadSurfaceBundles).toHaveBeenCalledWith({
      workspaceKey: 'acme',
      surfaceKey: 'kiosk',
      injectStyles: false,
    })
  })

  it('sagt es, wenn das Layout noch keiner Fläche zugeordnet ist', async () => {
    // Sonst wundert sich später jemand, warum ein Block fehlt: Geladen werden dann die Blöcke
    // der Standardfläche, und nichts auf der Seite sagt das.
    vi.stubGlobal('fetch', vi.fn(async () => draftResponse(null)))

    const wrapper = await loadLayout(null)

    expect(loadSurfaceBundles).toHaveBeenCalledWith(
      expect.objectContaining({ surfaceKey: undefined }),
    )
    expect(wrapper.text()).toContain('noch keiner Fläche zugeordnet')
  })

  it('nennt das Plugin, dessen Bundle nicht lud, statt nur Platzhalter zu zeigen', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => draftResponse('kiosk')))
    loadSurfaceBundles.mockResolvedValue({
      registry: {},
      results: [
        { pluginId: 'voip', scriptUrl: '/x.js', status: 'error', durationMs: 1, error: 'boom' },
        { pluginId: 'theme', scriptUrl: '/y.js', status: 'loaded', durationMs: 1 },
      ],
      styles: [],
    })

    const wrapper = await loadLayout('kiosk')

    expect(wrapper.text()).toContain('voip')
    expect(wrapper.text()).not.toContain('theme')
  })

  it('zeigt den Canvas erst, wenn ein Layout geladen ist', async () => {
    // Die Registry entsteht mit den Schlüsseln des Layouts und entsteht genau einmal. Früher
    // gerendert, hinge ihr Kontextkanal an „default" statt an der gestalteten Fläche.
    const wrapper = mount(ComposerAdminPage)

    expect(wrapper.find('.cal-canvas').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Noch keine Sektion')
  })

  it('reicht Stylesheet-Text und Theme-Werte an den Canvas durch', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => draftResponse('kiosk')))

    const wrapper = await loadLayout('kiosk')

    expect(fetchSurfaceStyles).toHaveBeenCalledWith(['/plugin-assets/voip/app/workspace/main.css'])
    expect(fetchThemeTokens).toHaveBeenCalledWith('acme')
    const canvas = wrapper.findComponent({ name: 'ComposerCanvas' })
    expect(canvas.props('surfaceCss')).toBe('.cal-header { color: red }')
    expect(canvas.props('tokens')).toEqual({ 'color-primary': '#123456' })
  })
})
