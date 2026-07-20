import type { SurfaceContext } from './surface-context'

/**
 * The client-side chain loader: it reads the workspace's ordered UI chain and the
 * published asset manifest, then injects the plugin bundles for this surface in chain
 * order. The bundles register their views into window.calloraSurface on load; the
 * reactive mounts (mountSurface) then render them — so loading runs AFTER mounting and
 * a late bundle still appears.
 *
 * Loading is fail-SOFT but not fail-silent: a missing manifest/chain or a broken bundle
 * leaves the surface degraded (empty), it never breaks the shell — but every outcome is
 * recorded per plugin (loaded/error + duration), logged, and exposed on
 * window.__calloraSurfaceLoad plus a `callora:surface-load` event so an operator can see
 * which bundle failed instead of facing a silently blank surface.
 */

export interface PluginAssetEntry {
  pluginId: string
  surface: string
  entryPath: string
}

export interface PluginStyleEntry {
  pluginId: string
  surface: string
  stylePath: string
}

export interface PluginManifest {
  entries?: PluginAssetEntry[]
  styleEntries?: PluginStyleEntry[]
}

/** A plugin's resolved bundle script, kept paired with its pluginId for telemetry. */
export interface ResolvedScript {
  pluginId: string
  url: string
}

export interface ResolvedSurfaceAssets {
  scripts: ResolvedScript[]
  styles: string[]
}

/** The outcome of loading one plugin bundle — the unit of load telemetry. */
export interface PluginLoadResult {
  pluginId: string
  scriptUrl: string
  status: 'loaded' | 'error'
  /** Wall-clock time from injection to load/error, in milliseconds. */
  durationMs: number
  /** Present only when status is 'error'. */
  error?: string
}

export interface PluginLoaderOptions {
  /** Which surface's assets to load; matches the manifest's `surface` field. */
  surface?: string
  /** Base path the static plugin assets are served under. */
  assetBase?: string
  /** The published asset manifest URL. */
  manifestUrl?: string
  /** The workspace UI-chain endpoint (ordered plugin ids for the workspace). */
  uiChainUrl?: string
}

export interface PluginLoaderDeps {
  /** Injectable JSON fetch (defaults to window.fetch) — the seam tests drive. */
  fetchJson?: (url: string) => Promise<unknown>
  doc?: Document
  /**
   * Injects a bundle script and resolves once it has executed, rejecting on load error.
   * Injectable because the test environment keeps injected <script src> inert (it never
   * fires load/error), so tests substitute a deterministic loader.
   */
  loadScript?: (doc: Document, src: string) => Promise<void>
  /** Monotonic clock for durations; injectable so tests are deterministic. */
  now?: () => number
}

const DEFAULTS = {
  surface: 'workspace',
  assetBase: '/plugin-assets',
  manifestUrl: '/manifests/plugin-ui-assets.manifest.json',
  uiChainUrl: '/workspace/public/ui-chain',
} as const

/** Where the per-load telemetry is published for operator/admin diagnosis. */
export const SURFACE_LOAD_GLOBAL = '__calloraSurfaceLoad'
/** The event dispatched on the document once loading has settled, carrying the results. */
export const SURFACE_LOAD_EVENT = 'callora:surface-load'

/**
 * Filters the manifest to this surface and to the plugins in the workspace's chain,
 * orders them by the chain, and turns entry/style paths into absolute asset URLs.
 * Scripts keep their pluginId (telemetry attributes failures to a plugin); styles are
 * non-blocking and stay bare URLs. Pure — no DOM, no I/O.
 */
export function resolveSurfaceAssets(
  manifest: PluginManifest,
  chain: string[],
  surface: string,
  assetBase: string,
): ResolvedSurfaceAssets {
  const order = new Map(chain.map((pluginId, index) => [pluginId, index]))
  const base = assetBase.replace(/\/+$/, '')
  const forSurface = <T extends { surface: string; pluginId: string }>(items: T[] | undefined) =>
    (items ?? [])
      .filter((item) => item.surface === surface && order.has(item.pluginId))
      .sort((a, b) => order.get(a.pluginId)! - order.get(b.pluginId)!)

  return {
    scripts: forSurface(manifest.entries)
      .filter((entry) => isSafeRelativePath(entry.entryPath))
      .map((entry) => ({ pluginId: entry.pluginId, url: `${base}/${entry.entryPath}` })),
    styles: forSurface(manifest.styleEntries)
      .filter((entry) => isSafeRelativePath(entry.stylePath))
      .map((entry) => `${base}/${entry.stylePath}`),
  }
}

/**
 * Defence in depth: an asset path must stay a same-origin path UNDER the base. The
 * manifest is server-published (trusted), but a bundle src must never point off the
 * plugin-assets root — reject a scheme (http:/javascript:), an absolute or protocol-
 * relative path (/, //, \) and any parent-traversal segment.
 */
function isSafeRelativePath(path: string): boolean {
  if (path.length === 0 || path.includes(':') || path.startsWith('/') || path.startsWith('\\')) {
    return false
  }
  return !path.split(/[/\\]/).includes('..')
}

/**
 * Injects the resolved stylesheet links into the document head. Idempotent (a URL
 * already present is skipped). Styles are non-blocking, so they are fire-and-forget —
 * unlike scripts, they carry no registration and need no load telemetry.
 */
export function injectSurfaceStyles(doc: Document, styles: string[]): void {
  const head = doc.head ?? doc.documentElement
  for (const href of styles) {
    if (hasElementWithAttr(doc, 'link', 'data-callora-plugin-style', href)) {
      continue
    }
    const link = doc.createElement('link')
    link.rel = 'stylesheet'
    link.href = href
    link.setAttribute('data-callora-plugin-style', href)
    head.appendChild(link)
  }
}

/**
 * Appends a bundle script (async=false so bundles run in chain order) unless one for the
 * same src is already present. Returns the created element, or null when a matching
 * script already exists. Split out from load-awaiting so the DOM mutation is unit-testable
 * without depending on browser load events.
 */
export function injectPluginScript(doc: Document, src: string): HTMLScriptElement | null {
  if (hasElementWithAttr(doc, 'script', 'data-callora-plugin-entry', src)) {
    return null
  }
  const head = doc.head ?? doc.documentElement
  const script = doc.createElement('script')
  script.src = src
  // Dynamically inserted scripts default to async; false preserves chain order so a
  // bundle that extends an earlier one runs after it.
  script.async = false
  script.setAttribute('data-callora-plugin-entry', src)
  head.appendChild(script)
  return script
}

/**
 * Fetches, resolves and loads this workspace's surface plugin bundles, returning one
 * PluginLoadResult per bundle (chain order). Never throws — discovery failures resolve
 * to an empty result set, a single bundle's load error is isolated to that plugin.
 */
export async function loadSurfacePlugins(
  context: SurfaceContext,
  options: PluginLoaderOptions = {},
  deps: PluginLoaderDeps = {},
): Promise<PluginLoadResult[]> {
  const surface = options.surface ?? DEFAULTS.surface
  const assetBase = options.assetBase ?? DEFAULTS.assetBase
  const manifestUrl = options.manifestUrl ?? DEFAULTS.manifestUrl
  const uiChainUrl = options.uiChainUrl ?? DEFAULTS.uiChainUrl
  const doc = deps.doc ?? document
  const fetchJson = deps.fetchJson ?? defaultFetchJson
  const loadScript = deps.loadScript ?? defaultLoadScript
  const now = deps.now ?? defaultNow

  let assets: ResolvedSurfaceAssets
  try {
    const chain = await fetchChain(fetchJson, uiChainUrl, context.workspaceKey)
    if (chain.length === 0) {
      return publishResults(doc, [])
    }
    const manifest = ((await fetchJson(manifestUrl)) ?? {}) as PluginManifest
    assets = resolveSurfaceAssets(manifest, chain, surface, assetBase)
  } catch (error) {
    // Discovery (chain/manifest) failed entirely — the surface renders whatever
    // registered (often nothing). Fail-soft, but recorded and logged, not silent.
    console.warn('[callora-surface] plugin discovery failed; rendering without plugins.', error)
    return publishResults(doc, [])
  }

  injectSurfaceStyles(doc, assets.styles)

  const results: PluginLoadResult[] = []
  for (const script of assets.scripts) {
    const startedAt = now()
    try {
      await loadScript(doc, script.url)
      results.push({
        pluginId: script.pluginId,
        scriptUrl: script.url,
        status: 'loaded',
        durationMs: now() - startedAt,
      })
    } catch (error) {
      // Fail-soft per plugin: one broken bundle must not stop the others or the shell,
      // but it is no longer swallowed — it is logged and surfaced in the telemetry.
      console.warn(
        `[callora-surface] plugin "${script.pluginId}" failed to load (${script.url}); ` +
          'continuing without it.',
        error,
      )
      results.push({
        pluginId: script.pluginId,
        scriptUrl: script.url,
        status: 'error',
        durationMs: now() - startedAt,
        error: error instanceof Error ? error.message : String(error),
      })
    }
  }

  return publishResults(doc, results)
}

/**
 * Default script loader: inject the script and resolve on its `load` event, reject on
 * `error`. An already-present script (idempotent re-entry) resolves immediately.
 */
function defaultLoadScript(doc: Document, src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const script = injectPluginScript(doc, src)
    if (script === null) {
      resolve()
      return
    }
    script.addEventListener('load', () => resolve())
    script.addEventListener('error', () =>
      reject(new Error(`Failed to load plugin script: ${src}`)),
    )
  })
}

/**
 * Publishes the load telemetry so an operator can diagnose a degraded surface: exposes
 * the results on window.__calloraSurfaceLoad and dispatches a `callora:surface-load`
 * event a shell can listen for to show a visible degraded state.
 */
function publishResults(doc: Document, results: PluginLoadResult[]): PluginLoadResult[] {
  const view = doc.defaultView as
    | (Window & { [SURFACE_LOAD_GLOBAL]?: PluginLoadResult[] })
    | null
  if (view) {
    view[SURFACE_LOAD_GLOBAL] = results
  }
  if (typeof doc.dispatchEvent === 'function' && typeof CustomEvent === 'function') {
    doc.dispatchEvent(new CustomEvent(SURFACE_LOAD_EVENT, { detail: { results } }))
  }
  return results
}

async function fetchChain(
  fetchJson: (url: string) => Promise<unknown>,
  uiChainUrl: string,
  workspaceKey: string,
): Promise<string[]> {
  const url = `${uiChainUrl}?workspaceKey=${encodeURIComponent(workspaceKey)}`
  const result = (await fetchJson(url)) as { chain?: unknown }
  return Array.isArray(result?.chain)
    ? result.chain.filter((id): id is string => typeof id === 'string')
    : []
}

async function defaultFetchJson(url: string): Promise<unknown> {
  const response = await fetch(url, { headers: { accept: 'application/json' } })
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

function hasElementWithAttr(doc: Document, tag: string, attr: string, value: string): boolean {
  return Array.from(doc.getElementsByTagName(tag)).some((el) => el.getAttribute(attr) === value)
}
