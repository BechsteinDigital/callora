import { defineComponent, h } from 'vue'
import { registerExtension } from './registry'
import { registerHook } from './hooks'
import { registerService } from './services'

// Backend manifest (published by PluginUiAssetPublisher) and asset root.
const MANIFEST_URL = '/manifests/plugin-ui-assets.manifest.json'
const ASSET_BASE = '/plugin-assets'
const SURFACE = 'admin'

export interface PluginUiManifestEntry {
  pluginId: string
  surface: string
  entryPath: string
}

export interface PluginUiStyleEntry {
  pluginId: string
  surface: string
  stylePath: string
}

export interface PluginUiManifest {
  entries?: PluginUiManifestEntry[]
  styleEntries?: PluginUiStyleEntry[]
}

// The global API a plugin bundle (a classic script, loaded at runtime) registers
// against — no build-time dependency on the shell. Vue primitives are shared so a
// plugin builds components without bundling its own Vue.
export interface CalloraAdminGlobal {
  registerExtension: typeof registerExtension
  registerHook: typeof registerHook
  registerService: typeof registerService
  vue: { h: typeof h; defineComponent: typeof defineComponent }
}

// "custom/plugins/<...>" → "<...>"; strips backslashes and leading slashes.
export function normalizeEntryPath(entryPath: string): string {
  const normalized = (entryPath ?? '').trim().replace(/\\/g, '/')
  if (!normalized) {
    return ''
  }
  const marker = 'custom/plugins/'
  const i = normalized.toLowerCase().indexOf(marker)
  if (i >= 0) {
    return normalized.slice(i + marker.length)
  }
  return normalized.replace(/^\/+/, '')
}

function resolveUrl(path: string): string {
  const rel = normalizeEntryPath(path)
  return rel ? `${ASSET_BASE}/${rel}` : ''
}

function isJavaScript(path: string): boolean {
  const p = normalizeEntryPath(path)
  return p.endsWith('.js') || p.endsWith('.mjs')
}

// Pure selection: the admin-surface script + style URLs from a manifest.
export function selectAdminAssets(manifest: PluginUiManifest): { scripts: string[]; styles: string[] } {
  // Total by design: a null/garbage manifest body (a malformed response) yields
  // empty selections rather than throwing into the bootstrap path.
  const styles = (manifest?.styleEntries ?? [])
    .filter((e) => e.surface === SURFACE)
    .map((e) => resolveUrl(e.stylePath))
    .filter(Boolean)
  const scripts = (manifest?.entries ?? [])
    .filter((e) => e.surface === SURFACE && isJavaScript(e.entryPath))
    .map((e) => resolveUrl(e.entryPath))
    .filter(Boolean)
  return { scripts, styles }
}

export function installGlobalApi(): void {
  const api: CalloraAdminGlobal = {
    registerExtension,
    registerHook,
    registerService,
    vue: { h, defineComponent },
  }
  ;(globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin = api
}

function appendStylesheet(url: string): void {
  if (!url || document.querySelector(`link[data-callora-plugin-style="${url}"]`)) {
    return
  }
  const link = document.createElement('link')
  link.rel = 'stylesheet'
  link.href = url
  link.dataset.calloraPluginStyle = url
  document.head.appendChild(link)
}

function appendScript(url: string): Promise<void> {
  if (!url || document.querySelector(`script[data-callora-plugin-entry="${url}"]`)) {
    return Promise.resolve()
  }
  return new Promise<void>((resolve, reject) => {
    const script = document.createElement('script')
    script.async = true
    script.src = url
    script.dataset.calloraPluginEntry = url
    script.onload = () => resolve()
    script.onerror = () => reject(new Error(`Failed to load plugin asset '${url}'.`))
    document.head.appendChild(script)
  })
}

// Loads plugin admin UI from the backend manifest. Installs the global API first
// so plugin scripts can register. A missing manifest is tolerated (early dev);
// one broken bundle does not block the others. Styles load first (cascade order),
// then scripts sequentially (a later bundle may extend an earlier one).
export async function loadPluginExtensions(): Promise<void> {
  installGlobalApi()

  let manifest: PluginUiManifest
  try {
    // A hung manifest server must not stall the bootstrap indefinitely; the
    // resulting AbortError falls into the catch below and is tolerated.
    const res = await fetch(MANIFEST_URL, { credentials: 'include', signal: AbortSignal.timeout(5000) })
    if (!res.ok) {
      return
    }
    manifest = (await res.json()) as PluginUiManifest
  } catch {
    return
  }

  const { scripts, styles } = selectAdminAssets(manifest)
  for (const url of styles) {
    try {
      appendStylesheet(url)
    } catch {
      // A malformed style URL must not block the other assets or the mount.
    }
  }
  for (const url of scripts) {
    try {
      await appendScript(url)
    } catch {
      // A broken plugin bundle must not block the others.
    }
  }
}
