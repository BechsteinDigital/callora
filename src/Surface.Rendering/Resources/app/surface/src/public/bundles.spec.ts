import { beforeEach, describe, expect, it, vi } from 'vitest'
import { h } from 'vue'
import { ensureSurfaceRegistry, loadSurfaceBundles } from './bundles'
import type { PluginManifest } from '../plugin-loader'

const manifest: PluginManifest = {
  entries: [
    { pluginId: 'voip', surface: 'surface', entryPath: 'voip/app/workspace/main.js' },
    { pluginId: 'voip', surface: 'admin', entryPath: 'voip/app/admin/main.js' },
  ],
  styleEntries: [
    { pluginId: 'voip', surface: 'surface', stylePath: 'voip/app/workspace/main.css' },
  ],
}

function fetchJson(chain: string[] = ['voip']) {
  return vi.fn(async (url: string) =>
    url.includes('ui-chain') ? { chain } : (manifest as unknown),
  )
}

beforeEach(() => {
  document.head.replaceChildren()
  delete (window as unknown as Record<string, unknown>).calloraSurface
  // Every bundle keeps vue external; the host publishes it. Present here so the
  // missing-Vue warning does not fire in tests that are not about it.
  ;(window as unknown as Record<string, unknown>).CalloraVue = {}
})

describe('ensureSurfaceRegistry', () => {
  it('creates the registry once and returns the same one afterwards', () => {
    const first = ensureSurfaceRegistry('acme', 'portal')
    const second = ensureSurfaceRegistry('acme', 'kiosk')

    expect(second).toBe(first)
    expect(window.calloraSurface).toBe(first)
  })

  it('keeps the blocks a second call would otherwise drop', () => {
    // The failure this guards is not "a block is missing now" but "a block is missing
    // for good": the loader skips a script already in the document, so a replaced
    // registry could never be refilled by re-injecting the bundle that filled it.
    const registry = ensureSurfaceRegistry('acme', 'portal')
    registry.blocks.registerBlock({
      id: 'demo.hero',
      label: 'Hero',
      category: 'content',
      component: { render: () => h('div') },
      controls: {},
    })

    const again = ensureSurfaceRegistry('acme', 'portal')

    expect(again.blocks.blocks.map((block) => block.id)).toEqual(['demo.hero'])
  })
})

describe('loadSurfaceBundles', () => {
  it('has the registry in place before the first bundle executes', async () => {
    // The reason this is one function rather than two the caller sequences: a bundle
    // that runs first registers into nothing, warns to the console, and leaves an empty
    // canvas with no error to find.
    const registryWhenScriptRan: unknown[] = []
    const loadScript = vi.fn(async () => {
      registryWhenScriptRan.push(window.calloraSurface)
    })

    await loadSurfaceBundles({ workspaceKey: 'acme', surfaceKey: 'portal' }, {
      fetchJson: fetchJson(),
      loadScript,
    })

    expect(loadScript).toHaveBeenCalledTimes(1)
    expect(registryWhenScriptRan[0]).toBeDefined()
  })

  it('asks the chain for the layout surface, not the workspace default', async () => {
    // An editor builds for one surface; loading the default surface's chain would offer
    // blocks that will not be there once the layout is live.
    const json = fetchJson()

    await loadSurfaceBundles({ workspaceKey: 'acme', surfaceKey: 'kiosk' }, {
      fetchJson: json,
      loadScript: vi.fn(async () => {}),
    })

    const chainUrl = json.mock.calls.map(([url]) => url).find((url) => url.includes('ui-chain'))
    expect(chainUrl).toContain('workspaceKey=acme')
    expect(chainUrl).toContain('surfaceKey=kiosk')
  })

  it('takes the named target surface instead of the surface bundles', async () => {
    const loaded: string[] = []

    await loadSurfaceBundles({ workspaceKey: 'acme', surface: 'admin' }, {
      fetchJson: fetchJson(),
      loadScript: vi.fn(async (_doc: Document, src: string) => {
        loaded.push(src)
      }),
    })

    expect(loaded).toEqual(['/plugin-assets/voip/app/admin/main.js'])
  })

  it('injects the stylesheets by default — on a surface they are the page', async () => {
    await loadSurfaceBundles({ workspaceKey: 'acme' }, {
      fetchJson: fetchJson(),
      loadScript: vi.fn(async () => {}),
    })

    expect(document.querySelectorAll('link[data-callora-plugin-style]')).toHaveLength(1)
  })

  it('keeps them out of an editor document, and hands back their URLs instead', async () => {
    // A surface stylesheet claims `.cal-header`, which means something on both sides. Left
    // to inject, it would restyle the admin shell AROUND the canvas — the escape the whole
    // scoping exists to prevent. The editor fetches the text and scopes it itself.
    const result = await loadSurfaceBundles({ workspaceKey: 'acme', injectStyles: false }, {
      fetchJson: fetchJson(),
      loadScript: vi.fn(async () => {}),
    })

    expect(document.querySelectorAll('link[data-callora-plugin-style]')).toHaveLength(0)
    expect(result.styles).toEqual(['/plugin-assets/voip/app/workspace/main.css'])
  })

  it('reports a broken bundle instead of failing the load', async () => {
    const result = await loadSurfaceBundles({ workspaceKey: 'acme' }, {
      fetchJson: fetchJson(),
      loadScript: vi.fn(async () => {
        throw new Error('404')
      }),
    })

    expect(result.results).toHaveLength(1)
    expect(result.results[0]).toMatchObject({ pluginId: 'voip', status: 'error' })
    expect(result.registry).toBe(window.calloraSurface)
  })

  it('returns an empty result set when discovery fails, and still a registry', async () => {
    const result = await loadSurfaceBundles({ workspaceKey: 'acme' }, {
      fetchJson: vi.fn(async () => {
        throw new Error('offline')
      }),
      loadScript: vi.fn(async () => {}),
    })

    expect(result.results).toEqual([])
    expect(result.registry).toBeDefined()
  })

  it('warns when the host published no Vue, because the bundle error names something else', async () => {
    delete (window as unknown as Record<string, unknown>).CalloraVue
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    await loadSurfaceBundles({ workspaceKey: 'acme' }, {
      fetchJson: fetchJson([]),
      loadScript: vi.fn(async () => {}),
    })

    expect(warn.mock.calls.some(([message]) => String(message).includes('CalloraVue'))).toBe(true)
    warn.mockRestore()
  })
})
