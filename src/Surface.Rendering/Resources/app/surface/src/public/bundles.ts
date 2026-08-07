import { createSurfaceRegistry, type SurfaceRegistry } from '../surface-registry'
import {
  loadSurfacePlugins,
  type PluginLoaderDeps,
  type PluginLoadResult,
} from '../plugin-loader'

/**
 * Loading a surface's plugin bundles — for hosts that are not the surface.
 *
 * The surface runtime does this at startup for the page it renders. An editor needs the
 * same thing for a surface it is *not* standing on: the composer's canvas renders the
 * real block components, which only exist once their plugin bundles have loaded, and the
 * admin shell's own loader only ever fetches assets whose manifest surface is `admin`.
 *
 * The composer could rebuild this — fetch the chain, fetch the manifest, inject scripts —
 * and that would be ordinary plugin work, not a special right. But every editor would
 * rebuild it, and the fault tolerance and the load telemetry (`__calloraSurfaceLoad`)
 * would exist twice. So it lives here, parameterised by target surface, and the runtime's
 * own startup goes through the same code.
 */

/** Which bundles to load, and from where. */
export interface SurfaceBundleOptions {
  /** The workspace whose chain decides which plugins are loaded at all. */
  workspaceKey: string
  /**
   * The surface being loaded for. Decides the chain — a surface can carry a different
   * set of plugins than the workspace default — and binds the registry's context channel.
   * Absent, the workspace's default surface decides, which is what an unbound layout gets.
   */
  surfaceKey?: string
  /**
   * Which manifest surface to take assets from. Defaults to `surface`, the block bundles.
   * Named rather than fixed because the same capability serves any target surface.
   */
  surface?: string
  /** Base path the static plugin assets are served under. */
  assetBase?: string
  /** The published asset manifest URL. */
  manifestUrl?: string
  /** The chain endpoint. Defaults to the public one, which an admin session also passes. */
  uiChainUrl?: string
  /**
   * Whether the plugin stylesheets go into this document. True on a surface, where they
   * ARE the page's styling.
   *
   * An editor sets it false. A surface stylesheet claims names like `.cal-header` that
   * mean something on both sides, so injecting it into the admin document would restyle
   * the shell around the canvas — the very escape `@scope` exists to prevent. The URLs
   * come back either way, so such a host fetches their text and scopes it itself.
   */
  injectStyles?: boolean
}

/** What loading produced: the registry the bundles registered into, and how each fared. */
export interface SurfaceBundleLoad {
  readonly registry: SurfaceRegistry
  /** One entry per bundle, in chain order. Empty when discovery found nothing. */
  readonly results: readonly PluginLoadResult[]
  /** The plugin stylesheet URLs, injected into this document or not. */
  readonly styles: readonly string[]
}

/**
 * Returns the surface registry, creating it if this document has none.
 *
 * Idempotent, and deliberately so in both directions: a second call never replaces the
 * registry, because replacing it would drop every block already registered while the
 * loader would refuse to re-inject the bundles that registered them (a script already in
 * the document is skipped) — the blocks would be gone for good, silently.
 *
 * The workspace/surface keys therefore bind the context channel on FIRST creation only.
 * An editor that switches to a layout on another surface keeps the channel it started
 * with; the blocks accumulate across surfaces, which is right, and the channel binding is
 * the editor's business once it publishes simulated values into it.
 */
export function ensureSurfaceRegistry(
  workspaceKey?: string,
  surfaceKey?: string,
): SurfaceRegistry {
  const existing = globalThis.window?.calloraSurface
  if (existing) {
    return existing
  }

  const created = createSurfaceRegistry(workspaceKey, surfaceKey)
  if (globalThis.window) {
    globalThis.window.calloraSurface = created
  }

  return created
}

/**
 * Ensures the registry exists, then loads the target surface's plugin bundles into this
 * document.
 *
 * The order is the whole reason this is one function rather than two exports the caller
 * sequences. A bundle that loads before the registry exists registers into nothing:
 * `registerBlock` warns to the console and returns, because a plugin must never break the
 * shell it is a guest in. The result is a canvas that is simply empty, with no error and
 * nothing to look at — the failure a contract should make unreachable rather than
 * document.
 *
 * Never throws. Discovery failure yields an empty result set; one broken bundle is
 * isolated to that plugin and reported in {@link SurfaceBundleLoad.results}.
 */
export async function loadSurfaceBundles(
  options: SurfaceBundleOptions,
  deps: PluginLoaderDeps = {},
): Promise<SurfaceBundleLoad> {
  const registry = ensureSurfaceRegistry(options.workspaceKey, options.surfaceKey)
  warnWhenVueIsMissing()

  const { results, styles } = await loadSurfacePlugins(
    { workspaceKey: options.workspaceKey, surfaceKey: options.surfaceKey },
    {
      surface: options.surface,
      assetBase: options.assetBase,
      manifestUrl: options.manifestUrl,
      uiChainUrl: options.uiChainUrl,
      injectStyles: options.injectStyles,
    },
    deps,
  )

  return { registry, results, styles }
}

/**
 * A block bundle keeps vue external and reaches for `window.CalloraVue`. Absent it, the
 * bundle throws while executing and registers nothing — the host sees a load error whose
 * message ("CalloraVue is not defined") says nothing about the actual mistake, which is
 * that the host never published its Vue instance. Naming it once here is the difference
 * between ten minutes and an afternoon.
 */
function warnWhenVueIsMissing(): void {
  if (globalThis.window && !globalThis.window.CalloraVue) {
    console.warn(
      '[callora-surface] window.CalloraVue is not set; block bundles keep vue external and ' +
        'will fail to execute. The host must publish its Vue instance before loading bundles.',
    )
  }
}
