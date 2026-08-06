export interface PluginNavItem {
    readonly pluginId: string;
    readonly id: string;
    readonly label: string;
    readonly to: string;
    readonly icon: string | null;
    readonly order: number;
}
export declare function usePluginNavigation(): {
    items: import("vue").Ref<{
        readonly pluginId: string;
        readonly id: string;
        readonly label: string;
        readonly to: string;
        readonly icon: string | null;
        readonly order: number;
    }[], PluginNavItem[] | {
        readonly pluginId: string;
        readonly id: string;
        readonly label: string;
        readonly to: string;
        readonly icon: string | null;
        readonly order: number;
    }[]>;
};
export declare function loadPluginNavigation(): Promise<void>;
export declare function resetPluginNavigation(): void;
//# sourceMappingURL=pluginNavigation.d.ts.map