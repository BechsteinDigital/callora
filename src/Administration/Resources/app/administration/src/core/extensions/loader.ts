import * as Vue from 'vue'
import type { Component } from 'vue'
import { registerExtension } from './registry'
import { registerHook, type HookContext } from './hooks'
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

// A resolved asset URL together with the plugin that owns it, so registrations
// made while it loads can be attributed and load failures reported per plugin.
export interface PluginUiAssetRef {
  url: string
  pluginId: string
}

// The global API a plugin bundle (a classic script, loaded at runtime) registers
// against — no build-time dependency on the shell. The owning pluginId is injected
// by the loader (authoritative), so a plugin only supplies the extension itself and
// an optional priority. Vue primitives are shared so a plugin builds components
// without bundling its own Vue.
//
// ATTRIBUTION LIMIT: registrations are attributed to a plugin only when made
// SYNCHRONOUSLY at bundle top-level (during the bundle's load window). A call
// deferred via setTimeout / dynamic import runs after the window closes and is
// recorded with pluginId null (indistinguishable from a host registration).
// Register at top-level to be attributed.
export interface CalloraAdminGlobal {
  registerExtension(slot: string, component: Component, order?: number): void
  registerHook<T>(name: string, handler: (ctx: HookContext<T>) => void | Promise<void>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: { priority?: number }): void
  // The host's full Vue runtime, shared so a plugin bundle builds real .vue SFCs
  // against the SAME Vue instance (Vue marked external, mapped to CalloraAdmin.vue).
  // A plugin must never bundle its own Vue — two runtimes break reactivity and
  // component instancing across the boundary.
  vue: typeof Vue
}

// The plugin whose bundle is currently executing; register* calls made during that
// window are attributed to it. Set around each sequential script load.
let currentPluginId: string | null = null

export type PluginUiLoadStatus = 'loaded' | 'failed'

// Per-bundle load outcome, surfaced in Plugin-Management so a silently dropped
// plugin UI (404, timeout, broken bundle) is diagnosable rather than invisible.
export interface PluginUiLoadResult {
  readonly pluginId: string
  readonly url: string
  readonly status: PluginUiLoadStatus
  readonly detail?: string
}

const loadResults: PluginUiLoadResult[] = []

export function getPluginUiLoadResults(): readonly PluginUiLoadResult[] {
  return [...loadResults]
}

export function resetPluginUiLoadResults(): void {
  loadResults.length = 0
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

// Pure selection: the admin-surface script + style assets from a manifest, each
// with its owning pluginId. Total by design: a null/garbage manifest body yields
// empty selections rather than throwing into the bootstrap path.
export function selectAdminAssets(manifest: PluginUiManifest): { scripts: PluginUiAssetRef[]; styles: PluginUiAssetRef[] } {
  const styles = (manifest?.styleEntries ?? [])
    .filter((e) => e.surface === SURFACE)
    .map((e) => ({ url: resolveUrl(e.stylePath), pluginId: e.pluginId }))
    .filter((ref) => ref.url)
  const scripts = (manifest?.entries ?? [])
    .filter((e) => e.surface === SURFACE && isJavaScript(e.entryPath))
    .map((e) => ({ url: resolveUrl(e.entryPath), pluginId: e.pluginId }))
    .filter((ref) => ref.url)
  return { scripts, styles }
}

export function installGlobalApi(): void {
  // Wrappers inject the currently-loading pluginId so registrations are attributed
  // without the plugin declaring (or spoofing) its own id.
  const api: CalloraAdminGlobal = {
    registerExtension: (slot, component, order) => registerExtension(slot, component, order, currentPluginId),
    registerHook: (name, handler, order) => registerHook(name, handler, order, currentPluginId),
    registerService: (key, implementation, meta) =>
      registerService(key, implementation, { pluginId: currentPluginId, priority: meta?.priority }),
    vue: Vue,
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
// one broken bundle does not block the others and is recorded as a load result.
// Styles load first (cascade order), then scripts sequentially — each attributed
// to its plugin so registrations are owned and failures are diagnosable.
export async function loadPluginExtensions(): Promise<void> {
  resetPluginUiLoadResults()
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
  for (const { url } of styles) {
    try {
      appendStylesheet(url)
    } catch {
      // A malformed style URL must not block the other assets or the mount.
    }
  }
  for (const { url, pluginId } of scripts) {
    currentPluginId = pluginId
    try {
      await appendScript(url)
      loadResults.push({ pluginId, url, status: 'loaded' })
    } catch (e) {
      // A broken plugin bundle must not block the others; record it for diagnosis.
      loadResults.push({ pluginId, url, status: 'failed', detail: (e as Error).message })
    } finally {
      currentPluginId = null
    }
  }
}
