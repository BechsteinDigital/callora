import { type ComputedRef, type Ref } from 'vue';
/** What the operator chose: an explicit mode, or "whatever the system says". */
export type ThemePreference = 'system' | 'light' | 'dark';
/** The colour scheme actually rendered once the system signal is resolved. */
export type ResolvedTheme = 'light' | 'dark';
export declare const THEME_STORAGE_KEY = "callora.admin.theme";
/**
 * Applies the persisted preference and starts following the system signal.
 * Call once during bootstrap, before the app mounts, so the first paint is
 * already in the right colour scheme.
 */
export declare function initTheme(): void;
export declare function useTheme(): {
    preference: Ref<ThemePreference>;
    resolved: ComputedRef<ResolvedTheme>;
    setPreference: (value: ThemePreference) => void;
    toggle: () => void;
};
/** Resets the module singleton — for tests only. */
export declare function resetTheme(): void;
//# sourceMappingURL=theme.d.ts.map