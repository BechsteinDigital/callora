import * as Vue from 'vue';
import type { Component } from 'vue';
import { type HookContext } from './hooks';
export interface PluginUiManifestEntry {
    pluginId: string;
    surface: string;
    entryPath: string;
    /** Short content hash appended as a ?v= cache-busting query when present. */
    contentHash?: string;
}
export interface PluginUiStyleEntry {
    pluginId: string;
    surface: string;
    stylePath: string;
    contentHash?: string;
}
export interface PluginUiManifest {
    entries?: PluginUiManifestEntry[];
    styleEntries?: PluginUiStyleEntry[];
}
/** A resolved bundle URL together with the plugin that owns it, so failures are attributable. */
export interface PluginUiAssetRef {
    url: string;
    pluginId: string;
}
export interface ResolvedAdminAssets {
    scripts: PluginUiAssetRef[];
    /** Stylesheets are non-blocking and carry no registration, so they stay bare URLs. */
    styles: string[];
}
export type PluginUiLoadStatus = 'loaded' | 'failed';
export interface PluginUiLoadResult {
    readonly pluginId: string;
    readonly url: string;
    readonly status: PluginUiLoadStatus;
    /** Wall-clock time from injection to load/error, in milliseconds. */
    readonly durationMs: number;
    /** Present only when status is 'failed'. */
    readonly detail?: string;
}
export interface PluginUiLoaderOptions {
    /** Workspace whose chain to load. Omitted for a workspace-bound session — the server knows it. */
    workspaceKey?: string;
    chainUrl?: string;
    manifestUrl?: string;
    assetBase?: string;
}
export interface PluginUiLoaderDeps {
    /** Injectable JSON fetch (defaults to window.fetch) — the seam tests drive. */
    fetchJson?: (url: string) => Promise<unknown>;
    doc?: Document;
    /**
     * Injects a bundle script and resolves once it has executed, rejecting on load error.
     * Injectable because the test environment keeps an injected <script src> inert — it never
     * fires load/error — so tests substitute a deterministic loader.
     */
    loadScript?: (doc: Document, src: string) => Promise<void>;
    /** Monotonic clock for durations; injectable so tests are deterministic. */
    now?: () => number;
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
    registerExtension(slot: string, component: Component, order?: number): void;
    registerHook<T>(name: string, handler: (ctx: HookContext<T>) => void | Promise<void>, order?: number): void;
    registerService<T>(key: string, implementation: T, meta?: {
        priority?: number;
    }): void;
    /** Read side of the slot registry, so a plugin can render into a slot it does not own. */
    getExtensions(slot: string): Component[];
    /**
     * The host's Vue runtime, shared so a plugin bundle builds real .vue SFCs against the SAME
     * Vue instance (Vue marked external, mapped to CalloraAdmin.vue). A plugin must never bundle
     * its own Vue — two runtimes break reactivity and component instancing across the boundary.
     *
     * This moves to the shared `Callora.vue` global once @callora/ui-core exists; until then the
     * shipped bundles resolve through here.
     */
    vue: typeof Vue;
}
export declare function getPluginUiLoadResults(): readonly PluginUiLoadResult[];
export declare function resetPluginUiLoadResults(): void;
/**
 * Defence in depth: an asset path must stay a same-origin path UNDER the base. The manifest is
 * server-published (trusted), but a bundle src must never point off the plugin-assets root —
 * reject a scheme (http:/javascript:), an absolute or protocol-relative path (/, //, \) and any
 * parent-traversal segment.
 */
export declare function isSafeAssetPath(path: string): boolean;
/**
 * Filters the manifest to the admin surface and to the plugins in the workspace's chain, orders
 * them by the chain, and turns entry/style paths into absolute asset URLs. Pure — no DOM, no I/O.
 * Total by design: a null or garbage manifest yields empty selections rather than throwing into
 * the bootstrap path.
 */
export declare function resolveAdminAssets(manifest: PluginUiManifest, chain: readonly string[], assetBase: string): ResolvedAdminAssets;
export declare function installGlobalApi(): void;
/**
 * Loads the admin UI bundles of the workspace's chained plugins. Never throws — a discovery
 * failure resolves to an empty result set, a single bundle's error is isolated to that plugin.
 */
export declare function loadPluginExtensions(options?: PluginUiLoaderOptions, deps?: PluginUiLoaderDeps): Promise<PluginUiLoadResult[]>;
//# sourceMappingURL=loader.d.ts.map