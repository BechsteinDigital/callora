import * as Vue from 'vue'
import type { Component } from 'vue'
import { getExtensions, registerExtension } from './registry'
import { registerSurfaceTab } from './surfaceTabs'
import { registerHook, type HookContext } from './hooks'
import { registerService } from './services'
import { t } from '@/core/i18n/i18n'

/**
 * The admin micro-frontend loader.
 *
 * It reads the workspace's UI chain and the published asset manifest, then injects the admin
 * bundles of exactly the plugins the server named — in chain order. The chain is the point:
 * before it, every admin bundle in the manifest was loaded regardless of whether the plugin
 * was assigned to the active workspace, which made the assignment ineffective on the UI layer.
 *
 * Loading is fail-SOFT but not fail-silent: a missing chain or a broken bundle leaves the shell
 * intact, but every outcome is recorded per plugin (loaded/failed + duration) so an operator can
 * see which bundle failed instead of facing a silently missing interface.
 */

const DEFAULTS = {
  chainUrl: '/api/ext/admin/ui-chain',
  manifestUrl: '/manifests/plugin-ui-assets.manifest.json',
  assetBase: '/plugin-assets',
} as const

const SURFACE = 'admin'
const FETCH_TIMEOUT_MS = 5000

export interface PluginUiManifestEntry {
  pluginId: string
  surface: string
  entryPath: string
  /** Short content hash appended as a ?v= cache-busting query when present. */
  contentHash?: string
}

export interface PluginUiStyleEntry {
  pluginId: string
  surface: string
  stylePath: string
  contentHash?: string
}

export interface PluginUiManifest {
  entries?: PluginUiManifestEntry[]
  styleEntries?: PluginUiStyleEntry[]
}

/** A resolved bundle URL together with the plugin that owns it, so failures are attributable. */
export interface PluginUiAssetRef {
  url: string
  pluginId: string
}

export interface ResolvedAdminAssets {
  scripts: PluginUiAssetRef[]
  /** Stylesheets are non-blocking and carry no registration, so they stay bare URLs. */
  styles: string[]
}

export type PluginUiLoadStatus = 'loaded' | 'failed'

export interface PluginUiLoadResult {
  readonly pluginId: string
  readonly url: string
  readonly status: PluginUiLoadStatus
  /** Wall-clock time from injection to load/error, in milliseconds. */
  readonly durationMs: number
  /** Present only when status is 'failed'. */
  readonly detail?: string
}

export interface PluginUiLoaderOptions {
  /** Workspace whose chain to load. Omitted for a workspace-bound session — the server knows it. */
  workspaceKey?: string
  chainUrl?: string
  manifestUrl?: string
  assetBase?: string
}

export interface PluginUiLoaderDeps {
  /** Injectable JSON fetch (defaults to window.fetch) — the seam tests drive. */
  fetchJson?: (url: string) => Promise<unknown>
  doc?: Document
  /**
   * Injects a bundle script and resolves once it has executed, rejecting on load error.
   * Injectable because the test environment keeps an injected <script src> inert — it never
   * fires load/error — so tests substitute a deterministic loader.
   */
  loadScript?: (doc: Document, src: string) => Promise<void>
  /** Monotonic clock for durations; injectable so tests are deterministic. */
  now?: () => number
}

/**
 * The global API a plugin bundle (a classic script, loaded at runtime) registers against — no
 * build-time dependency on the shell. The owning pluginId is injected by the loader
 * (authoritative), so a plugin only supplies the extension itself and an optional priority.
 *
 * ATTRIBUTION LIMIT: registrations are attributed to a plugin only when made SYNCHRONOUSLY at
 * bundle top-level (during the bundle's load window). A call deferred via setTimeout / dynamic
 * import runs after the window closes and is recorded with pluginId null (indistinguishable
 * from a host registration). Register at top-level to be attributed.
 */
export interface CalloraAdminGlobal {
  registerExtension(slot: string, component: Component, order?: number): void
  /**
   * Ein Reiter an der Fläche, der DIESE App zugewiesen ist. Kein Slot, weil er eine
   * Beschriftung trägt und nur dort erscheint, wo seine App steht.
   */
  registerSurfaceTab(id: string, label: string, component: Component, order?: number): void
  registerHook<T>(name: string, handler: (ctx: HookContext<T>) => void | Promise<void>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: { priority?: number }): void
  /** Read side of the slot registry, so a plugin can render into a slot it does not own. */
  getExtensions(slot: string): Component[]
  /**
   * Resolves a snippet key against the shell's loaded texts, falling back to the text passed in.
   *
   * Here rather than in the plugin, for the same reason Vue is: the shell holds the loaded
   * snippets, and a plugin bundling its own vue-i18n would get a second instance that knows none
   * of them — a translation function that always returns the fallback and never says why.
   */
  translate(key: string, fallback: string): string
}

// The plugin whose bundle is currently executing; register* calls made during that window are
// attributed to it. Set around each sequential script load.
let currentPluginId: string | null = null

const loadResults: PluginUiLoadResult[] = []

export function getPluginUiLoadResults(): readonly PluginUiLoadResult[] {
  return [...loadResults]
}

export function resetPluginUiLoadResults(): void {
  loadResults.length = 0
}

/**
 * Defence in depth: an asset path must stay a same-origin path UNDER the base. The manifest is
 * server-published (trusted), but a bundle src must never point off the plugin-assets root —
 * reject a scheme (http:/javascript:), an absolute or protocol-relative path (/, //, \) and any
 * parent-traversal segment.
 */
export function isSafeAssetPath(path: string): boolean {
  if (!path || path.includes(':') || path.startsWith('/') || path.startsWith('\\')) {
    return false
  }
  return !path.split(/[/\\]/).includes('..')
}

/**
 * Appends the published content hash as a `?v=` cache-busting query. An upgraded bundle hashes
 * differently, so its URL changes and a stale copy is never reused. A hash-less entry yields the
 * bare URL, which then relies on revalidation.
 */
function withVersion(url: string, contentHash?: string): string {
  return contentHash ? `${url}?v=${encodeURIComponent(contentHash)}` : url
}

/**
 * Filters the manifest to the admin surface and to the plugins in the workspace's chain, orders
 * them by the chain, and turns entry/style paths into absolute asset URLs. Pure — no DOM, no I/O.
 * Total by design: a null or garbage manifest yields empty selections rather than throwing into
 * the bootstrap path.
 */
export function resolveAdminAssets(
  manifest: PluginUiManifest,
  chain: readonly string[],
  assetBase: string,
): ResolvedAdminAssets {
  const order = new Map(chain.map((pluginId, index) => [pluginId, index]))
  const base = assetBase.replace(/\/+$/, '')
  const forChain = <T extends { surface: string; pluginId: string }>(items: T[] | undefined) =>
    (Array.isArray(items) ? items : [])
      .filter((item) => item?.surface === SURFACE && order.has(item.pluginId))
      .sort((a, b) => order.get(a.pluginId)! - order.get(b.pluginId)!)

  return {
    scripts: forChain(manifest?.entries)
      .filter((entry) => isSafeAssetPath(entry.entryPath) && isJavaScript(entry.entryPath))
      .map((entry) => ({
        pluginId: entry.pluginId,
        url: withVersion(`${base}/${entry.entryPath}`, entry.contentHash),
      })),
    styles: forChain(manifest?.styleEntries)
      .filter((entry) => isSafeAssetPath(entry.stylePath))
      .map((entry) => withVersion(`${base}/${entry.stylePath}`, entry.contentHash)),
  }
}

function isJavaScript(path: string): boolean {
  return path.endsWith('.js') || path.endsWith('.mjs')
}

export function installGlobalApi(): void {
  // Wrappers inject the currently-loading pluginId so registrations are attributed without the
  // plugin declaring (or spoofing) its own id.
  const api: CalloraAdminGlobal = {
    registerExtension: (slot, component, order) => registerExtension(slot, component, order, currentPluginId),
    registerSurfaceTab: (id, label, component, order) =>
      registerSurfaceTab(id, label, component, order, currentPluginId),
    registerHook: (name, handler, order) => registerHook(name, handler, order, currentPluginId),
    registerService: (key, implementation, meta) =>
      registerService(key, implementation, { pluginId: currentPluginId, priority: meta?.priority }),
    getExtensions,
    translate: t,
  }
  ;(globalThis as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin = api

  // The shared Vue instance, under the name BOTH runtimes use. It used to live inside
  // CalloraAdmin, which meant a bundle was built for exactly one of the two shells: the
  // composer's canvas runs surface blocks inside the admin, and a block built against
  // CalloraAdmin.vue could never run on a surface, nor the other way round.
  //
  // A plugin must never bundle its own Vue. Two runtimes do not fail loudly — they fail by
  // reactivity quietly not crossing the boundary.
  ;(globalThis as unknown as { CalloraVue?: typeof Vue }).CalloraVue = Vue
}

function injectStylesheet(doc: Document, url: string): void {
  const head = doc.head ?? doc.documentElement
  if (doc.querySelector(`link[data-callora-plugin-style="${url}"]`)) {
    return
  }
  const link = doc.createElement('link')
  link.rel = 'stylesheet'
  link.href = url
  link.setAttribute('data-callora-plugin-style', url)
  head.appendChild(link)
}

/**
 * Default script loader: inject the script and resolve on its `load` event, reject on `error`.
 * An already-present script (idempotent re-entry) resolves immediately. `async = false` preserves
 * chain order so a bundle that extends an earlier one runs after it.
 */
function defaultLoadScript(doc: Document, src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    if (doc.querySelector(`script[data-callora-plugin-entry="${src}"]`)) {
      resolve()
      return
    }
    const head = doc.head ?? doc.documentElement
    const script = doc.createElement('script')
    script.src = src
    script.async = false
    script.setAttribute('data-callora-plugin-entry', src)
    script.addEventListener('load', () => resolve())
    script.addEventListener('error', () => reject(new Error(`Failed to load plugin asset '${src}'.`)))
    head.appendChild(script)
  })
}

async function defaultFetchJson(url: string): Promise<unknown> {
  // A hung server must not stall the bootstrap indefinitely; the resulting AbortError is
  // tolerated by the caller.
  const response = await fetch(url, {
    credentials: 'include',
    headers: { accept: 'application/json' },
    signal: AbortSignal.timeout(FETCH_TIMEOUT_MS),
  })
  if (!response.ok) {
    throw new Error(`Fetch failed (${response.status}) for ${url}`)
  }
  return response.json()
}

function defaultNow(): number {
  return typeof performance !== 'undefined' && typeof performance.now === 'function'
    ? performance.now()
    : Date.now()
}

async function fetchChain(
  fetchJson: (url: string) => Promise<unknown>,
  chainUrl: string,
  workspaceKey?: string,
): Promise<string[]> {
  // Without a workspaceKey the server uses the caller's bound workspace; a platform operator
  // selects one. Either way the server decides — the client never widens its own chain.
  const url = workspaceKey ? `${chainUrl}?workspaceKey=${encodeURIComponent(workspaceKey)}` : chainUrl
  const result = (await fetchJson(url)) as { chain?: unknown } | null
  return Array.isArray(result?.chain) ? result.chain.filter((id): id is string => typeof id === 'string') : []
}

/**
 * Loads the admin UI bundles of the workspace's chained plugins. Never throws — a discovery
 * failure resolves to an empty result set, a single bundle's error is isolated to that plugin.
 */
export async function loadPluginExtensions(
  options: PluginUiLoaderOptions = {},
  deps: PluginUiLoaderDeps = {},
): Promise<PluginUiLoadResult[]> {
  const chainUrl = options.chainUrl ?? DEFAULTS.chainUrl
  const manifestUrl = options.manifestUrl ?? DEFAULTS.manifestUrl
  const assetBase = options.assetBase ?? DEFAULTS.assetBase
  const doc = deps.doc ?? document
  const fetchJson = deps.fetchJson ?? defaultFetchJson
  const loadScript = deps.loadScript ?? defaultLoadScript
  const now = deps.now ?? defaultNow

  resetPluginUiLoadResults()
  installGlobalApi()

  let assets: ResolvedAdminAssets
  try {
    const chain = await fetchChain(fetchJson, chainUrl, options.workspaceKey)
    if (chain.length === 0) {
      return []
    }
    const manifest = ((await fetchJson(manifestUrl)) ?? {}) as PluginUiManifest
    assets = resolveAdminAssets(manifest, chain, assetBase)
  } catch (error) {
    // Discovery failed entirely — the shell renders without plugin UI. Fail-soft, but logged:
    // loading nothing is the safe direction, loading everything would not be.
    console.warn('[callora-admin] plugin discovery failed; rendering without plugin UI.', error)
    return []
  }

  for (const url of assets.styles) {
    try {
      injectStylesheet(doc, url)
    } catch {
      // A malformed style URL must not block the other assets or the mount.
    }
  }

  for (const script of assets.scripts) {
    const startedAt = now()
    currentPluginId = script.pluginId
    try {
      await loadScript(doc, script.url)
      loadResults.push({
        pluginId: script.pluginId,
        url: script.url,
        status: 'loaded',
        durationMs: now() - startedAt,
      })
    } catch (error) {
      // Fail-soft per plugin: one broken bundle must not stop the others or the shell.
      console.warn(
        `[callora-admin] plugin "${script.pluginId}" failed to load (${script.url}); continuing without it.`,
        error,
      )
      loadResults.push({
        pluginId: script.pluginId,
        url: script.url,
        status: 'failed',
        durationMs: now() - startedAt,
        detail: error instanceof Error ? error.message : String(error),
      })
    } finally {
      currentPluginId = null
    }
  }

  return [...loadResults]
}
