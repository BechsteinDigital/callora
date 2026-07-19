import type { SurfaceContext } from './surface-context'

/**
 * The client-side chain loader: it reads the workspace's ordered UI chain and the
 * published asset manifest, then injects the plugin bundles for this surface in chain
 * order. The bundles register their views into window.calloraSurface on load; the
 * reactive mounts (mountSurface) then render them — so loading runs AFTER mounting and
 * a late bundle still appears. Every failure is tolerated: a missing manifest/chain or
 * a broken bundle leaves the surface empty, it never breaks the shell.
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

export interface ResolvedSurfaceAssets {
  scripts: string[]
  styles: string[]
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
}

const DEFAULTS = {
  surface: 'workspace',
  assetBase: '/plugin-assets',
  manifestUrl: '/manifests/plugin-ui-assets.manifest.json',
  uiChainUrl: '/workspace/public/ui-chain',
} as const

/**
 * Filters the manifest to this surface and to the plugins in the workspace's chain,
 * orders them by the chain, and turns entry/style paths into absolute asset URLs.
 * Pure — no DOM, no I/O.
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
      .map((entry) => `${base}/${entry.entryPath}`),
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
 * Injects the resolved stylesheet links and plugin scripts into the document head.
 * Idempotent (a URL already present is skipped) and order-preserving: scripts use
 * async=false so dynamically inserted bundles still execute in chain order.
 */
export function injectSurfaceAssets(doc: Document, assets: ResolvedSurfaceAssets): void {
  const head = doc.head ?? doc.documentElement

  for (const href of assets.styles) {
    if (hasElementWithAttr(doc, 'link', 'data-callora-plugin-style', href)) {
      continue
    }
    const link = doc.createElement('link')
    link.rel = 'stylesheet'
    link.href = href
    link.setAttribute('data-callora-plugin-style', href)
    head.appendChild(link)
  }

  for (const src of assets.scripts) {
    if (hasElementWithAttr(doc, 'script', 'data-callora-plugin-entry', src)) {
      continue
    }
    const script = doc.createElement('script')
    script.src = src
    // Dynamically inserted scripts default to async; false preserves chain order so a
    // bundle that extends an earlier one runs after it.
    script.async = false
    script.setAttribute('data-callora-plugin-entry', src)
    head.appendChild(script)
  }
}

/** Fetches, resolves and injects this workspace's surface plugin bundles. Never throws. */
export async function loadSurfacePlugins(
  context: SurfaceContext,
  options: PluginLoaderOptions = {},
  deps: PluginLoaderDeps = {},
): Promise<void> {
  const surface = options.surface ?? DEFAULTS.surface
  const assetBase = options.assetBase ?? DEFAULTS.assetBase
  const manifestUrl = options.manifestUrl ?? DEFAULTS.manifestUrl
  const uiChainUrl = options.uiChainUrl ?? DEFAULTS.uiChainUrl
  const doc = deps.doc ?? document
  const fetchJson = deps.fetchJson ?? defaultFetchJson

  try {
    const chain = await fetchChain(fetchJson, uiChainUrl, context.workspaceKey)
    if (chain.length === 0) {
      return
    }

    const manifest = ((await fetchJson(manifestUrl)) ?? {}) as PluginManifest
    injectSurfaceAssets(doc, resolveSurfaceAssets(manifest, chain, surface, assetBase))
  } catch (error) {
    // A missing manifest/chain, an offline server or a malformed response must never
    // break the shell — the surface simply renders whatever registered (often empty).
    console.warn('[callora-surface] plugin loading failed; rendering without plugins.', error)
  }
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

function hasElementWithAttr(doc: Document, tag: string, attr: string, value: string): boolean {
  return Array.from(doc.getElementsByTagName(tag)).some((el) => el.getAttribute(attr) === value)
}
