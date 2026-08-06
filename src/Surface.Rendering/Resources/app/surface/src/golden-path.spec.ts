import { describe, it, expect, vi, beforeEach } from 'vitest'
import * as Vue from 'vue'
import { nextTick } from 'vue'
import { createSurfaceRegistry } from './surface-registry'
import { mountSurface } from './mount'
import { injectPluginScript, loadSurfacePlugins } from './plugin-loader'
// The real, committed bundle of the SurfaceDemo reference plugin, imported as raw text
// (built by the @callora/surface-sdk preset: Vue external → CalloraVue, registerSurfaceView).
// The path sits outside this project's root, so vite.config.ts allowlists it explicitly —
// Vite's filesystem guard denies such ids by default since the path-traversal hardening.
import surfaceDemoBundle from '../../../../../../custom/plugins/SurfaceDemo/src/Resources/public/surface/main.js?raw'

const guestCaller = {
  state: 'guest' as const,
  subject: { issuer: 'callora.surface-guest', subjectId: '' },
}

/**
 * End-to-end golden path, exercised with the REAL reference-plugin bundle: chain loader →
 * asset injection → the plugin bundle executes and registers its view → the runtime mount
 * renders it. In this unit environment the injected `<script src>` is inert (no server), so
 * the bundle is executed directly — the same code the browser would run — instead of fetched.
 */
describe('surface golden path (real SurfaceDemo bundle)', () => {
  beforeEach(() => {
    document.body.replaceChildren()
    document.head.replaceChildren()
    delete (window as unknown as { calloraSurface?: unknown }).calloraSurface
    delete (window as unknown as { CalloraVue?: unknown }).CalloraVue
  })

  it('loads the chain, the plugin bundle registers its view, and the runtime renders it', async () => {
    // 1. The SSR shell root + the shared runtime state (CalloraVue + the registry).
    document.body.innerHTML =
      '<div id="callora-app" data-workspace="acme" data-surface="portal"></div>'
    const registry = createSurfaceRegistry()
    ;(window as unknown as { CalloraVue: typeof Vue }).CalloraVue = Vue
    ;(window as unknown as { calloraSurface: typeof registry }).calloraSurface = registry

    // 2. The app mounts — empty until a plugin contributes a view.
    mountSurface(registry)
    expect(document.querySelector('.surface-demo')).toBeNull()

    // 3. The chain loader fetches the workspace UI chain + the asset manifest and injects
    //    the plugin's bundle script in chain order.
    const fetchJson = vi.fn(async (url: string) =>
      url.startsWith('/workspace/public/ui-chain')
        ? { chain: ['surface-demo'] }
        : {
            entries: [
              {
                pluginId: 'surface-demo',
                surface: 'surface',
                entryPath: 'surface-demo/app/surface/main.js',
              },
            ],
          },
    )
    // The unit environment never fires a real <script> load event, so drive the loader
    // with a browser-faithful seam: it injects the tag (so the src below is observable)
    // and resolves — the bundle itself is executed in step 4, as the browser would on load.
    const loadScript = async (doc: Document, src: string) => {
      injectPluginScript(doc, src)
    }
    const results = await loadSurfacePlugins(
      { workspaceKey: 'acme', surfaceKey: 'portal', caller: guestCaller },
      {},
      { fetchJson, loadScript },
    )
    expect(results).toEqual([
      {
        pluginId: 'surface-demo',
        scriptUrl: '/plugin-assets/surface-demo/app/surface/main.js',
        status: 'loaded',
        durationMs: expect.any(Number),
      },
    ])

    // 3a. The loader resolved + injected exactly the chain plugin's bundle.
    const injected = document.querySelector('script[data-callora-plugin-entry]')
    expect(injected?.getAttribute('src')).toBe(
      '/plugin-assets/surface-demo/app/surface/main.js',
    )

    // 4. The bundle executes (browser would run it on load): it registers surface-demo.greeting.
    new Function(surfaceDemoBundle)()
    expect(registry.views.map((v) => v.id)).toContain('surface-demo.greeting')

    // 5. The already-mounted app renders the newly registered view — reactively, with the
    //    resolved SurfaceContext (workspace/surface) passed through.
    await nextTick()
    const view = document.querySelector('.surface-demo')
    expect(view).not.toBeNull()
    expect(view?.textContent).toContain('acme')
    expect(view?.textContent).toContain('portal')
    expect(document.querySelector('[data-testid="counter"]')).not.toBeNull()
  })
})
