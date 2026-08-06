import { describe, it, expect, vi, beforeEach } from 'vitest'
import {
  injectPluginScript,
  injectSurfaceStyles,
  loadSurfacePlugins,
  resolveSurfaceAssets,
  SURFACE_LOAD_EVENT,
  SURFACE_LOAD_GLOBAL,
  type PluginLoadResult,
  type PluginManifest,
} from './plugin-loader'

const guestCaller = {
  state: 'guest' as const,
  subject: { issuer: 'callora.surface-guest', subjectId: '' },
}

// The environment (vite.config: disableCSSFileLoading/disableJavaScriptFileLoading)
// keeps injected tags inert; clear the head so each injection test starts clean.
beforeEach(() => {
  document.head.replaceChildren()
  delete (window as unknown as Record<string, unknown>)[SURFACE_LOAD_GLOBAL]
})

const manifest: PluginManifest = {
  entries: [
    { pluginId: 'voip', surface: 'surface', entryPath: 'voip/app/workspace/main.js' },
    { pluginId: 'theme', surface: 'surface', entryPath: 'theme/app/workspace/main.js' },
    { pluginId: 'voip', surface: 'admin', entryPath: 'voip/app/admin/main.js' },
    { pluginId: 'notInChain', surface: 'surface', entryPath: 'x/app/workspace/main.js' },
  ],
  styleEntries: [
    { pluginId: 'voip', surface: 'surface', stylePath: 'voip/app/workspace/main.css' },
    { pluginId: 'voip', surface: 'admin', stylePath: 'voip/app/admin/main.css' },
  ],
}

describe('resolveSurfaceAssets', () => {
  it('keeps only the surface + chain plugins, orders by chain, builds URLs with pluginId', () => {
    // chain order (theme before voip) must win over manifest order (voip before theme).
    const assets = resolveSurfaceAssets(manifest, ['theme', 'voip'], 'surface', '/plugin-assets')

    expect(assets.scripts).toEqual([
      { pluginId: 'theme', url: '/plugin-assets/theme/app/workspace/main.js' },
      { pluginId: 'voip', url: '/plugin-assets/voip/app/workspace/main.js' },
    ])
    expect(assets.styles).toEqual(['/plugin-assets/voip/app/workspace/main.css'])
  })

  it('drops entries of other surfaces and plugins not in the chain', () => {
    const assets = resolveSurfaceAssets(manifest, ['voip'], 'surface', '/plugin-assets')

    expect(assets.scripts.map((s) => s.url)).toEqual(['/plugin-assets/voip/app/workspace/main.js'])
    expect(assets.scripts.some((s) => s.url.includes('/admin/'))).toBe(false)
    expect(assets.scripts.some((s) => s.pluginId === 'notInChain')).toBe(false)
  })

  it('trims a trailing slash off the asset base and tolerates a missing manifest section', () => {
    const assets = resolveSurfaceAssets({ entries: undefined }, ['voip'], 'surface', '/plugin-assets/')

    expect(assets).toEqual({ scripts: [], styles: [] })
  })

  it('appends the content hash as a ?v= cache-busting query when present', () => {
    const withHash: PluginManifest = {
      entries: [
        { pluginId: 'voip', surface: 'surface', entryPath: 'voip/app/workspace/main.js', contentHash: 'abc123' },
        // No contentHash → bare URL (legacy manifest / unhashable file).
        { pluginId: 'theme', surface: 'surface', entryPath: 'theme/app/workspace/main.js' },
      ],
      styleEntries: [
        { pluginId: 'voip', surface: 'surface', stylePath: 'voip/app/workspace/main.css', contentHash: 'def456' },
      ],
    }

    const assets = resolveSurfaceAssets(withHash, ['voip', 'theme'], 'surface', '/plugin-assets')

    expect(assets.scripts).toEqual([
      { pluginId: 'voip', url: '/plugin-assets/voip/app/workspace/main.js?v=abc123' },
      { pluginId: 'theme', url: '/plugin-assets/theme/app/workspace/main.js' },
    ])
    expect(assets.styles).toEqual(['/plugin-assets/voip/app/workspace/main.css?v=def456'])
  })

  it('drops entries whose path escapes the base (scheme, absolute, protocol-relative, traversal)', () => {
    const evil: PluginManifest = {
      entries: [
        { pluginId: 'ok', surface: 'surface', entryPath: 'ok/app/workspace/main.js' },
        { pluginId: 'scheme', surface: 'surface', entryPath: 'https://evil.example/x.js' },
        { pluginId: 'protoRel', surface: 'surface', entryPath: '//evil.example/x.js' },
        { pluginId: 'absolute', surface: 'surface', entryPath: '/etc/passwd.js' },
        { pluginId: 'traversal', surface: 'surface', entryPath: '../../secret.js' },
      ],
    }

    const assets = resolveSurfaceAssets(
      evil,
      ['ok', 'scheme', 'protoRel', 'absolute', 'traversal'],
      'surface',
      '/plugin-assets',
    )

    expect(assets.scripts).toEqual([{ pluginId: 'ok', url: '/plugin-assets/ok/app/workspace/main.js' }])
  })
})

describe('injectSurfaceStyles', () => {
  it('injects style links with a tracking attribute', () => {
    injectSurfaceStyles(document, ['/plugin-assets/a.css', '/plugin-assets/b.css'])

    const links = Array.from(document.querySelectorAll('link[data-callora-plugin-style]'))
    expect(links.map((l) => l.getAttribute('href'))).toEqual([
      '/plugin-assets/a.css',
      '/plugin-assets/b.css',
    ])
  })

  it('is idempotent — re-injecting the same URL does not duplicate the link', () => {
    injectSurfaceStyles(document, ['/plugin-assets/a.css'])
    injectSurfaceStyles(document, ['/plugin-assets/a.css'])

    expect(document.querySelectorAll('link[data-callora-plugin-style]')).toHaveLength(1)
  })
})

describe('injectPluginScript', () => {
  it('appends a chain-ordered script (async=false) with a tracking attribute', () => {
    const script = injectPluginScript(document, '/plugin-assets/a.js')

    expect(script).not.toBeNull()
    expect(script!.getAttribute('src')).toBe('/plugin-assets/a.js')
    expect(script!.async).toBe(false)
    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(1)
  })

  it('is idempotent — a second call for the same src returns null and adds no duplicate', () => {
    injectPluginScript(document, '/plugin-assets/a.js')
    const second = injectPluginScript(document, '/plugin-assets/a.js')

    expect(second).toBeNull()
    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(1)
  })
})

describe('loadSurfacePlugins', () => {
  const context = { workspaceKey: 'acme', surfaceKey: 'portal', caller: guestCaller }
  // A loader that behaves like the browser's: it injects the script (so DOM order is
  // observable) and resolves, unless the src is in `failing`, where it rejects.
  const injectingLoader = (failing: string[] = []) =>
    vi.fn(async (doc: Document, src: string) => {
      injectPluginScript(doc, src)
      if (failing.some((f) => src.includes(f))) {
        throw new Error(`boom: ${src}`)
      }
    })

  it('loads the chain + manifest bundles in order and returns a loaded result per plugin', async () => {
    const fetchJson = vi.fn(async (url: string) =>
      url.startsWith('/workspace/public/ui-chain') ? { chain: ['theme', 'voip'] } : manifest,
    )
    let clock = 0
    const now = () => (clock += 5)

    const results = await loadSurfacePlugins(
      context,
      {},
      { fetchJson, doc: document, loadScript: injectingLoader(), now },
    )

    // The context carries a surfaceKey, so it is passed through for the per-surface gate.
    expect(fetchJson).toHaveBeenCalledWith(
      '/workspace/public/ui-chain?workspaceKey=acme&surfaceKey=portal',
    )
    const scripts = Array.from(document.querySelectorAll('script[data-callora-plugin-entry]'))
    expect(scripts.map((s) => s.getAttribute('src'))).toEqual([
      '/plugin-assets/theme/app/workspace/main.js',
      '/plugin-assets/voip/app/workspace/main.js',
    ])
    expect(results).toEqual([
      {
        pluginId: 'theme',
        scriptUrl: '/plugin-assets/theme/app/workspace/main.js',
        status: 'loaded',
        durationMs: 5,
      },
      {
        pluginId: 'voip',
        scriptUrl: '/plugin-assets/voip/app/workspace/main.js',
        status: 'loaded',
        durationMs: 5,
      },
    ])
  })

  it('omits surfaceKey from the chain URL when the context has none (workspace-wide gate)', async () => {
    const fetchJson = vi.fn(async (url: string) =>
      url.startsWith('/workspace/public/ui-chain') ? { chain: ['voip'] } : manifest,
    )

    await loadSurfacePlugins(
      { workspaceKey: 'acme', surfaceKey: '', caller: guestCaller },
      {},
      { fetchJson, doc: document, loadScript: injectingLoader() },
    )

    expect(fetchJson).toHaveBeenCalledWith('/workspace/public/ui-chain?workspaceKey=acme')
  })

  it('encodes the surfaceKey when appending it to the chain URL', async () => {
    const fetchJson = vi.fn(async (url: string) =>
      url.startsWith('/workspace/public/ui-chain') ? { chain: ['voip'] } : manifest,
    )

    await loadSurfacePlugins(
      { workspaceKey: 'acme', surfaceKey: 'a b/c', caller: guestCaller },
      {},
      { fetchJson, doc: document, loadScript: injectingLoader() },
    )

    expect(fetchJson).toHaveBeenCalledWith(
      '/workspace/public/ui-chain?workspaceKey=acme&surfaceKey=a%20b%2Fc',
    )
  })

  it('isolates a failing bundle: it is recorded as error, logged, and the rest still load', async () => {
    const fetchJson = vi.fn(async (url: string) =>
      url.startsWith('/workspace/public/ui-chain') ? { chain: ['theme', 'voip'] } : manifest,
    )
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    const results = await loadSurfacePlugins(
      context,
      {},
      { fetchJson, doc: document, loadScript: injectingLoader(['theme']) },
    )

    expect(results.map((r) => [r.pluginId, r.status])).toEqual([
      ['theme', 'error'],
      ['voip', 'loaded'],
    ])
    expect(results[0].error).toContain('boom')
    // The failure is surfaced, not swallowed: one warn naming the failed plugin.
    expect(warn).toHaveBeenCalledTimes(1)
    expect(warn.mock.calls[0][0]).toContain('theme')

    warn.mockRestore()
  })

  it('publishes the telemetry globally and dispatches a settle event', async () => {
    const fetchJson = vi.fn(async (url: string) =>
      url.startsWith('/workspace/public/ui-chain') ? { chain: ['voip'] } : manifest,
    )
    const events: PluginLoadResult[][] = []
    // once: true so the listener does not leak into the sibling tests that also dispatch.
    document.addEventListener(
      SURFACE_LOAD_EVENT,
      (e) => events.push((e as CustomEvent).detail.results),
      { once: true },
    )

    const results = await loadSurfacePlugins(
      context,
      {},
      { fetchJson, doc: document, loadScript: injectingLoader() },
    )

    expect((window as unknown as Record<string, PluginLoadResult[]>)[SURFACE_LOAD_GLOBAL]).toBe(
      results,
    )
    expect(events).toHaveLength(1)
    expect(events[0].map((r) => r.pluginId)).toEqual(['voip'])
  })

  it('injects nothing and returns an empty result set when the chain is empty', async () => {
    const fetchJson = vi.fn(async () => ({ chain: [] }))

    const results = await loadSurfacePlugins(context, {}, { fetchJson, doc: document })

    expect(results).toEqual([])
    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(0)
  })

  it('tolerates a failing discovery fetch — never throws, warns, returns empty', async () => {
    const fetchJson = vi.fn(async () => {
      throw new Error('offline')
    })
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    await expect(
      loadSurfacePlugins(context, {}, { fetchJson, doc: document }),
    ).resolves.toEqual([])
    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(0)
    expect(warn).toHaveBeenCalledTimes(1)

    warn.mockRestore()
  })
})
