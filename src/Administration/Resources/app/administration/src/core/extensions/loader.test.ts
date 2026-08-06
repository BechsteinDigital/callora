import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import {
  isSafeAssetPath,
  resolveAdminAssets,
  installGlobalApi,
  loadPluginExtensions,
  getPluginUiLoadResults,
  resetPluginUiLoadResults,
  type CalloraAdminGlobal,
  type PluginUiLoaderDeps,
  type PluginUiManifest,
} from './loader'
import { registerService, getServiceConflicts, resetServices } from './services'

function globalApi(): CalloraAdminGlobal | undefined {
  return (globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
}

/**
 * Injectable dependencies for a deterministic load: the chain and manifest come from a map
 * instead of the network, and scripts "load" without the browser events happy-dom never fires
 * for an injected <script src>.
 */
function deps(
  responses: Record<string, unknown>,
  options: { failing?: string[]; onScript?: (url: string) => void } = {},
): PluginUiLoaderDeps {
  let clock = 0
  return {
    fetchJson: async (url: string) => {
      const key = Object.keys(responses).find((candidate) => url.startsWith(candidate))
      if (key === undefined) {
        throw new Error(`unexpected fetch: ${url}`)
      }
      return responses[key]
    },
    loadScript: async (_doc: Document, src: string) => {
      options.onScript?.(src)
      if (options.failing?.some((fragment) => src.includes(fragment))) {
        throw new Error(`Failed to load plugin asset '${src}'.`)
      }
    },
    now: () => (clock += 5),
  }
}

const CHAIN = '/api/ext/admin/ui-chain'
const MANIFEST = '/manifests/plugin-ui-assets.manifest.json'

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  resetPluginUiLoadResults()
  delete (globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
})

describe('isSafeAssetPath', () => {
  it('accepts a plain relative path under the asset base', () => {
    expect(isSafeAssetPath('acme/app/admin/main.js')).toBe(true)
  })

  it('rejects a scheme, so a manifest can never point a bundle off-origin', () => {
    expect(isSafeAssetPath('https://evil.example/x.js')).toBe(false)
    expect(isSafeAssetPath('javascript:alert(1)')).toBe(false)
  })

  it('rejects absolute and protocol-relative paths', () => {
    expect(isSafeAssetPath('/etc/passwd')).toBe(false)
    expect(isSafeAssetPath('//evil.example/x.js')).toBe(false)
    expect(isSafeAssetPath('\\evil')).toBe(false)
  })

  it('rejects parent traversal in any segment', () => {
    expect(isSafeAssetPath('acme/../../secret.js')).toBe(false)
    expect(isSafeAssetPath('acme\\..\\secret.js')).toBe(false)
  })

  it('rejects an empty path', () => {
    expect(isSafeAssetPath('')).toBe(false)
  })
})

describe('resolveAdminAssets', () => {
  const manifest: PluginUiManifest = {
    entries: [
      { pluginId: 'a', surface: 'admin', entryPath: 'a/app/admin/main.js' },
      { pluginId: 'a', surface: 'surface', entryPath: 'a/app/surface/main.js' },
      { pluginId: 'b', surface: 'admin', entryPath: 'b/app/admin/main.js' },
    ],
    styleEntries: [
      { pluginId: 'a', surface: 'admin', stylePath: 'a/app/admin/main.css' },
      { pluginId: 'a', surface: 'surface', stylePath: 'a/app/surface/main.css' },
    ],
  }

  it('selects only admin-surface assets of plugins the chain names', () => {
    const { scripts, styles } = resolveAdminAssets(manifest, ['a'], '/plugin-assets')

    expect(scripts).toEqual([{ url: '/plugin-assets/a/app/admin/main.js', pluginId: 'a' }])
    expect(styles).toEqual(['/plugin-assets/a/app/admin/main.css'])
  })

  it('drops a plugin the chain does not name, which is the whole point of the chain', () => {
    const { scripts } = resolveAdminAssets(manifest, ['a'], '/plugin-assets')

    expect(scripts.map((s) => s.pluginId)).not.toContain('b')
  })

  it('orders scripts by the chain, so a bundle extending an earlier one runs after it', () => {
    const { scripts } = resolveAdminAssets(manifest, ['b', 'a'], '/plugin-assets')

    expect(scripts.map((s) => s.pluginId)).toEqual(['b', 'a'])
  })

  it('appends the content hash as a cache-busting query', () => {
    const hashed: PluginUiManifest = {
      entries: [{ pluginId: 'a', surface: 'admin', entryPath: 'a/app/admin/main.js', contentHash: 'deadbeef' }],
      styleEntries: [{ pluginId: 'a', surface: 'admin', stylePath: 'a/app/admin/main.css', contentHash: 'cafe' }],
    }

    const { scripts, styles } = resolveAdminAssets(hashed, ['a'], '/plugin-assets')

    expect(scripts[0].url).toBe('/plugin-assets/a/app/admin/main.js?v=deadbeef')
    expect(styles[0]).toBe('/plugin-assets/a/app/admin/main.css?v=cafe')
  })

  it('rejects an unsafe asset path even though the manifest is server-published', () => {
    const hostile: PluginUiManifest = {
      entries: [{ pluginId: 'a', surface: 'admin', entryPath: 'https://evil.example/x.js' }],
    }

    expect(resolveAdminAssets(hostile, ['a'], '/plugin-assets').scripts).toEqual([])
  })

  it('ignores admin entries that are not JavaScript', () => {
    const mixed: PluginUiManifest = {
      entries: [
        { pluginId: 'a', surface: 'admin', entryPath: 'a/app/admin/main.css' },
        { pluginId: 'a', surface: 'admin', entryPath: 'a/app/admin/main.mjs' },
      ],
    }

    expect(resolveAdminAssets(mixed, ['a'], '/plugin-assets').scripts).toEqual([
      { url: '/plugin-assets/a/app/admin/main.mjs', pluginId: 'a' },
    ])
  })

  it('is total: a null or garbage manifest yields empty selections', () => {
    expect(resolveAdminAssets(null as unknown as PluginUiManifest, ['a'], '/plugin-assets')).toEqual({
      scripts: [],
      styles: [],
    })
    expect(resolveAdminAssets(42 as unknown as PluginUiManifest, ['a'], '/plugin-assets')).toEqual({
      scripts: [],
      styles: [],
    })
  })

  it('yields nothing for an empty chain', () => {
    expect(resolveAdminAssets(manifest, [], '/plugin-assets').scripts).toEqual([])
  })
})

describe('installGlobalApi', () => {
  it('exposes the register functions, the slot read side and the shared Vue on globalThis', () => {
    installGlobalApi()
    const api = globalApi()

    expect(typeof api?.registerExtension).toBe('function')
    expect(typeof api?.registerHook).toBe('function')
    expect(typeof api?.registerService).toBe('function')
    expect(typeof api?.getExtensions).toBe('function')
    // Shipped plugin bundles keep Vue external and resolve it here; removing this before
    // @callora/ui-core provides the shared global would break them.
    expect(typeof api?.vue.h).toBe('function')
    expect(typeof api?.vue.defineComponent).toBe('function')
  })
})

describe('loadPluginExtensions', () => {
  beforeEach(() => {
    delete (globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
  })

  it('installs the global API before loading, so a bundle can register on execution', async () => {
    await loadPluginExtensions({}, deps({ [CHAIN]: { chain: [] } }))

    expect(globalApi()).toBeDefined()
  })

  it('loads nothing when the chain is empty — an unassigned plugin gets no admin UI', async () => {
    const loaded: string[] = []
    await loadPluginExtensions({}, deps({ [CHAIN]: { chain: [] } }, { onScript: (u) => loaded.push(u) }))

    expect(loaded).toEqual([])
  })

  it('loads only the bundles of plugins in the chain', async () => {
    const loaded: string[] = []
    await loadPluginExtensions(
      {},
      deps(
        {
          [CHAIN]: { chain: ['a'] },
          [MANIFEST]: {
            entries: [
              { pluginId: 'a', surface: 'admin', entryPath: 'a/app/admin/main.js' },
              { pluginId: 'b', surface: 'admin', entryPath: 'b/app/admin/main.js' },
            ],
          },
        },
        { onScript: (u) => loaded.push(u) },
      ),
    )

    expect(loaded).toEqual(['/plugin-assets/a/app/admin/main.js'])
  })

  it('tolerates a failing chain request and loads nothing rather than everything', async () => {
    const loaded: string[] = []
    const failing: PluginUiLoaderDeps = {
      fetchJson: async () => {
        throw new Error('network down')
      },
      loadScript: async (_doc, src) => {
        loaded.push(src)
      },
    }

    await expect(loadPluginExtensions({}, failing)).resolves.toEqual([])
    expect(loaded).toEqual([])
    expect(globalApi()).toBeDefined()
  })

  it('tolerates a malformed chain body', async () => {
    await expect(loadPluginExtensions({}, deps({ [CHAIN]: null }))).resolves.toEqual([])
  })

  it('records a loaded result with a duration per bundle', async () => {
    const results = await loadPluginExtensions(
      {},
      deps({
        [CHAIN]: { chain: ['acme'] },
        [MANIFEST]: { entries: [{ pluginId: 'acme', surface: 'admin', entryPath: 'acme/app/admin/main.js' }] },
      }),
    )

    expect(results).toHaveLength(1)
    expect(results[0]).toMatchObject({ pluginId: 'acme', status: 'loaded' })
    expect(results[0].durationMs).toBeGreaterThan(0)
    expect(getPluginUiLoadResults()).toEqual(results)
  })

  it('records a failed result for a broken bundle without stopping the others', async () => {
    const results = await loadPluginExtensions(
      {},
      deps(
        {
          [CHAIN]: { chain: ['broken', 'good'] },
          [MANIFEST]: {
            entries: [
              { pluginId: 'broken', surface: 'admin', entryPath: 'broken/app/admin/main.js' },
              { pluginId: 'good', surface: 'admin', entryPath: 'good/app/admin/main.js' },
            ],
          },
        },
        { failing: ['broken/'] },
      ),
    )

    expect(results.map((r) => [r.pluginId, r.status])).toEqual([
      ['broken', 'failed'],
      ['good', 'loaded'],
    ])
    expect(results[0].detail).toContain('broken')
  })

  it('attributes a plugin service registration to the loading plugin and reports the conflict', async () => {
    resetServices()
    // A host default is already registered; the plugin overrides it during its load window.
    registerService('usersApi', { name: 'host' })

    await loadPluginExtensions(
      {},
      deps(
        {
          [CHAIN]: { chain: ['acme'] },
          [MANIFEST]: { entries: [{ pluginId: 'acme', surface: 'admin', entryPath: 'acme/app/admin/main.js' }] },
        },
        { onScript: () => globalApi()?.registerService('usersApi', { name: 'acme' }) },
      ),
    )

    expect(getServiceConflicts()).toEqual([
      { key: 'usersApi', activePluginId: 'acme', shadowedPluginIds: [null] },
    ])
    resetServices()
  })

  it('passes the selected workspace to the chain endpoint', async () => {
    const seen: string[] = []
    await loadPluginExtensions(
      { workspaceKey: 'ws-42' },
      {
        fetchJson: async (url: string) => {
          seen.push(url)
          return { chain: [] }
        },
      },
    )

    expect(seen[0]).toBe('/api/ext/admin/ui-chain?workspaceKey=ws-42')
  })

  it('omits the workspace query when none is selected, letting the server use the bound one', async () => {
    const seen: string[] = []
    await loadPluginExtensions(
      {},
      {
        fetchJson: async (url: string) => {
          seen.push(url)
          return { chain: [] }
        },
      },
    )

    expect(seen[0]).toBe('/api/ext/admin/ui-chain')
  })
})
