import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import {
  normalizeEntryPath,
  selectAdminAssets,
  installGlobalApi,
  loadPluginExtensions,
  type CalloraAdminGlobal,
  type PluginUiManifest,
} from './loader'

function globalApi(): CalloraAdminGlobal | undefined {
  return (globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  delete (globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
})

describe('normalizeEntryPath', () => {
  it('strips the custom/plugins/ marker and keeps the remainder', () => {
    expect(normalizeEntryPath('custom/plugins/acme/admin.js')).toBe('acme/admin.js')
  })

  it('is case-insensitive on the marker and normalizes backslashes', () => {
    expect(normalizeEntryPath('Custom\\Plugins\\acme\\admin.js')).toBe('acme/admin.js')
  })

  it('strips leading slashes when no marker is present', () => {
    expect(normalizeEntryPath('/acme/admin.js')).toBe('acme/admin.js')
  })

  it('returns empty for empty or whitespace input', () => {
    expect(normalizeEntryPath('')).toBe('')
    expect(normalizeEntryPath('   ')).toBe('')
  })
})

describe('selectAdminAssets', () => {
  it('selects only admin-surface scripts and styles and resolves them under the asset base', () => {
    const manifest: PluginUiManifest = {
      entries: [
        { pluginId: 'a', surface: 'admin', entryPath: 'custom/plugins/a/admin.js' },
        { pluginId: 'a', surface: 'workspace', entryPath: 'custom/plugins/a/store.js' },
      ],
      styleEntries: [
        { pluginId: 'a', surface: 'admin', stylePath: 'custom/plugins/a/admin.css' },
        { pluginId: 'a', surface: 'workspace', stylePath: 'custom/plugins/a/store.css' },
      ],
    }
    const { scripts, styles } = selectAdminAssets(manifest)
    expect(scripts).toEqual(['/plugin-assets/a/admin.js'])
    expect(styles).toEqual(['/plugin-assets/a/admin.css'])
  })

  it('ignores admin entries that are not JavaScript', () => {
    const manifest: PluginUiManifest = {
      entries: [
        { pluginId: 'a', surface: 'admin', entryPath: 'custom/plugins/a/admin.css' },
        { pluginId: 'a', surface: 'admin', entryPath: 'custom/plugins/a/admin.mjs' },
      ],
    }
    expect(selectAdminAssets(manifest).scripts).toEqual(['/plugin-assets/a/admin.mjs'])
  })

  it('tolerates a manifest without entries or styleEntries', () => {
    expect(selectAdminAssets({})).toEqual({ scripts: [], styles: [] })
  })

  it('is total: a null/garbage manifest body yields empty selections', () => {
    expect(selectAdminAssets(null as unknown as PluginUiManifest)).toEqual({ scripts: [], styles: [] })
    expect(selectAdminAssets(42 as unknown as PluginUiManifest)).toEqual({ scripts: [], styles: [] })
  })
})

describe('installGlobalApi', () => {
  it('exposes the register functions and shared Vue primitives on globalThis', () => {
    installGlobalApi()
    const api = globalApi()
    expect(typeof api?.registerExtension).toBe('function')
    expect(typeof api?.registerHook).toBe('function')
    expect(typeof api?.registerService).toBe('function')
    expect(typeof api?.vue.h).toBe('function')
    expect(typeof api?.vue.defineComponent).toBe('function')
  })
})

describe('loadPluginExtensions', () => {
  beforeEach(() => {
    delete (globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
  })

  it('installs the global API even when the manifest is missing (404)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }))
    await loadPluginExtensions()
    expect(globalApi()).toBeDefined()
  })

  it('tolerates a failing fetch without throwing', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network down')))
    await expect(loadPluginExtensions()).resolves.toBeUndefined()
    expect(globalApi()).toBeDefined()
  })

  it('passes an abort timeout signal to the manifest fetch and tolerates its rejection', async () => {
    const fetchMock = vi.fn().mockRejectedValue(new DOMException('timeout', 'TimeoutError'))
    vi.stubGlobal('fetch', fetchMock)
    await expect(loadPluginExtensions()).resolves.toBeUndefined()
    expect(fetchMock).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    )
  })

  it('tolerates a malformed (null) manifest body without throwing', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(null) }))
    await expect(loadPluginExtensions()).resolves.toBeUndefined()
  })

  it('appends the admin style and script assets from the manifest', async () => {
    const manifest: PluginUiManifest = {
      entries: [{ pluginId: 'a', surface: 'admin', entryPath: 'custom/plugins/a/admin.js' }],
      styleEntries: [{ pluginId: 'a', surface: 'admin', stylePath: 'custom/plugins/a/admin.css' }],
    }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(manifest) }))

    // happy-dom never fires onload for a real <script src>, so resolve the load
    // promise ourselves as soon as the element is appended.
    const appended: Array<{ tag: string; url: string }> = []
    vi.spyOn(document.head, 'appendChild').mockImplementation((node: unknown) => {
      const el = node as HTMLScriptElement & HTMLLinkElement
      if (el.tagName === 'SCRIPT') {
        appended.push({ tag: 'SCRIPT', url: el.src })
        Promise.resolve().then(() => el.onload?.(new Event('load')))
      } else if (el.tagName === 'LINK') {
        appended.push({ tag: 'LINK', url: el.href })
      }
      return node as Node
    })

    await loadPluginExtensions()

    expect(appended).toContainEqual({ tag: 'LINK', url: expect.stringContaining('/plugin-assets/a/admin.css') })
    expect(appended).toContainEqual({ tag: 'SCRIPT', url: expect.stringContaining('/plugin-assets/a/admin.js') })
  })

  it('does not let a broken plugin bundle reject the whole load', async () => {
    const manifest: PluginUiManifest = {
      entries: [{ pluginId: 'a', surface: 'admin', entryPath: 'custom/plugins/a/broken.js' }],
    }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(manifest) }))
    vi.spyOn(document.head, 'appendChild').mockImplementation((node: unknown) => {
      const el = node as HTMLScriptElement
      if (el.tagName === 'SCRIPT') {
        Promise.resolve().then(() => el.onerror?.(new Event('error')))
      }
      return node as Node
    })

    await expect(loadPluginExtensions()).resolves.toBeUndefined()
  })
})
