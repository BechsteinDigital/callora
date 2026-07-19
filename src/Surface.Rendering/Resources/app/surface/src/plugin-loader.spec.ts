import { describe, it, expect, vi, beforeEach } from 'vitest'
import {
  injectSurfaceAssets,
  loadSurfacePlugins,
  resolveSurfaceAssets,
  type PluginManifest,
} from './plugin-loader'

// The environment (vite.config: disableCSSFileLoading/disableJavaScriptFileLoading)
// keeps injected tags inert; clear the head so each injection test starts clean.
beforeEach(() => document.head.replaceChildren())

const manifest: PluginManifest = {
  entries: [
    { pluginId: 'voip', surface: 'workspace', entryPath: 'voip/app/workspace/main.js' },
    { pluginId: 'theme', surface: 'workspace', entryPath: 'theme/app/workspace/main.js' },
    { pluginId: 'voip', surface: 'admin', entryPath: 'voip/app/admin/main.js' },
    { pluginId: 'notInChain', surface: 'workspace', entryPath: 'x/app/workspace/main.js' },
  ],
  styleEntries: [
    { pluginId: 'voip', surface: 'workspace', stylePath: 'voip/app/workspace/main.css' },
    { pluginId: 'voip', surface: 'admin', stylePath: 'voip/app/admin/main.css' },
  ],
}

describe('resolveSurfaceAssets', () => {
  it('keeps only the surface + chain plugins, orders by chain, builds URLs', () => {
    // chain order (theme before voip) must win over manifest order (voip before theme).
    const assets = resolveSurfaceAssets(manifest, ['theme', 'voip'], 'workspace', '/plugin-assets')

    expect(assets.scripts).toEqual([
      '/plugin-assets/theme/app/workspace/main.js',
      '/plugin-assets/voip/app/workspace/main.js',
    ])
    expect(assets.styles).toEqual(['/plugin-assets/voip/app/workspace/main.css'])
  })

  it('drops entries of other surfaces and plugins not in the chain', () => {
    const assets = resolveSurfaceAssets(manifest, ['voip'], 'workspace', '/plugin-assets')

    expect(assets.scripts).toEqual(['/plugin-assets/voip/app/workspace/main.js'])
    expect(assets.scripts.some((u) => u.includes('/admin/'))).toBe(false)
    expect(assets.scripts.some((u) => u.includes('notInChain'))).toBe(false)
  })

  it('trims a trailing slash off the asset base and tolerates a missing manifest section', () => {
    const assets = resolveSurfaceAssets({ entries: undefined }, ['voip'], 'workspace', '/plugin-assets/')

    expect(assets).toEqual({ scripts: [], styles: [] })
  })

  it('drops entries whose path escapes the base (scheme, absolute, protocol-relative, traversal)', () => {
    const evil: PluginManifest = {
      entries: [
        { pluginId: 'ok', surface: 'workspace', entryPath: 'ok/app/workspace/main.js' },
        { pluginId: 'scheme', surface: 'workspace', entryPath: 'https://evil.example/x.js' },
        { pluginId: 'protoRel', surface: 'workspace', entryPath: '//evil.example/x.js' },
        { pluginId: 'absolute', surface: 'workspace', entryPath: '/etc/passwd.js' },
        { pluginId: 'traversal', surface: 'workspace', entryPath: '../../secret.js' },
      ],
    }

    const assets = resolveSurfaceAssets(
      evil,
      ['ok', 'scheme', 'protoRel', 'absolute', 'traversal'],
      'workspace',
      '/plugin-assets',
    )

    expect(assets.scripts).toEqual(['/plugin-assets/ok/app/workspace/main.js'])
  })
})

describe('injectSurfaceAssets', () => {
  it('injects ordered scripts (async=false) and style links with tracking attributes', () => {
    injectSurfaceAssets(document, {
      scripts: ['/plugin-assets/a.js', '/plugin-assets/b.js'],
      styles: ['/plugin-assets/a.css'],
    })

    const scripts = Array.from(document.querySelectorAll('script[data-callora-plugin-entry]'))
    expect(scripts.map((s) => s.getAttribute('src'))).toEqual([
      '/plugin-assets/a.js',
      '/plugin-assets/b.js',
    ])
    expect((scripts[0] as HTMLScriptElement).async).toBe(false)
    expect(document.querySelector('link[data-callora-plugin-style]')?.getAttribute('href')).toBe(
      '/plugin-assets/a.css',
    )
  })

  it('is idempotent — re-injecting the same URLs does not duplicate tags', () => {
    const assets = { scripts: ['/plugin-assets/a.js'], styles: ['/plugin-assets/a.css'] }

    injectSurfaceAssets(document, assets)
    injectSurfaceAssets(document, assets)

    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(1)
    expect(document.querySelectorAll('link[data-callora-plugin-style]')).toHaveLength(1)
  })
})

describe('loadSurfacePlugins', () => {
  const context = { workspaceKey: 'acme', surfaceKey: 'portal' }

  it('fetches the chain + manifest and injects the surface bundles in order', async () => {
    const fetchJson = vi.fn(async (url: string) => {
      if (url.startsWith('/workspace/public/ui-chain')) {
        return { workspaceKey: 'acme', chain: ['theme', 'voip'] }
      }
      return manifest
    })

    await loadSurfacePlugins(context, {}, { fetchJson, doc: document })

    expect(fetchJson).toHaveBeenCalledWith('/workspace/public/ui-chain?workspaceKey=acme')
    const scripts = Array.from(document.querySelectorAll('script[data-callora-plugin-entry]'))
    expect(scripts.map((s) => s.getAttribute('src'))).toEqual([
      '/plugin-assets/theme/app/workspace/main.js',
      '/plugin-assets/voip/app/workspace/main.js',
    ])
  })

  it('injects nothing when the chain is empty', async () => {
    const fetchJson = vi.fn(async () => ({ chain: [] }))

    await loadSurfacePlugins(context, {}, { fetchJson, doc: document })

    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(0)
  })

  it('tolerates a failing fetch — never throws, injects nothing', async () => {
    const fetchJson = vi.fn(async () => {
      throw new Error('offline')
    })
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    await expect(loadSurfacePlugins(context, {}, { fetchJson, doc: document })).resolves.toBeUndefined()
    expect(document.querySelectorAll('script[data-callora-plugin-entry]')).toHaveLength(0)

    warn.mockRestore()
  })
})
