import type { Component } from 'vue';
/**
 * Component replacement — deliberately NOT blanket override.
 *
 * Shopware-style `Component.override` couples a plugin to the internal structure of whatever it
 * overrides, which is exactly what the slot registry avoids ("additive slots, no
 * internal-structure coupling"). Here a component declares that it is replaceable, and its prop
 * contract is the boundary: a replacement must satisfy the same props, and TypeScript says so.
 *
 * Where a named slot suffices, that stays the better answer — replacement is the exception, for
 * the case where the whole rendering has to differ.
 *
 * Mirrors the service registry deliberately: exclusive (one implementation wins),
 * priority-ordered, and conflicts surfaced rather than swallowed.
 */
/**
 * A replaceable component: its key and the implementation to fall back on.
 *
 * Deliberately a wrapper rather than a branded component. Branding would mean writing the key
 * onto the component object, which mutates the caller's argument — and a component registered
 * under two keys would silently keep only the last one. A wrapper also makes the intent visible:
 * a token is not something you render, it is something you resolve.
 */
export interface ReplaceableComponent<T extends Component> {
    readonly key: string;
    readonly base: T;
}
export interface ReplacementMeta {
    /** Owning plugin, set by the loader. Null for a host or test registration. */
    readonly pluginId?: string | null;
    /** Highest priority wins; ties resolve to the last registration. */
    readonly priority?: number;
}
/**
 * Declares a component replaceable under a key. The returned token carries both, so a consumer
 * resolves without repeating the key — and cannot accidentally pass a different one.
 */
export declare function defineReplaceable<T extends Component>(key: string, base: T): ReplaceableComponent<T>;
export declare function replaceComponent(key: string, implementation: Component, meta?: ReplacementMeta): void;
/** Resolves the component to render: the winning replacement, or the declared original. */
export declare function useComponent<T extends Component>(token: ReplaceableComponent<T>): Component;
export interface ComponentConflict {
    readonly key: string;
    readonly activePluginId: string | null;
    readonly shadowedPluginIds: (string | null)[];
}
/**
 * A key replaced by more than one plugin. Two plugins replacing the same component is a
 * composition mistake somebody has to be able to see — swallowing it would leave an operator
 * wondering why one plugin's UI never appears.
 */
export declare function getComponentConflicts(): ComponentConflict[];
/** Test/hot-reload aid — clears all replacements. */
export declare function resetReplacements(): void;
//# sourceMappingURL=replaceable.d.ts.map