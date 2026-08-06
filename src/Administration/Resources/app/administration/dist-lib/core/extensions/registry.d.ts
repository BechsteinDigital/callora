import type { Component } from 'vue';
export interface ExtensionRegistration {
    readonly slot: string;
    readonly component: Component;
    readonly order: number;
    readonly pluginId: string | null;
}
export declare function registerExtension(slot: string, component: Component, order?: number, pluginId?: string | null): void;
export declare function getExtensions(slot: string): Component[];
export declare function resetExtensions(): void;
//# sourceMappingURL=registry.d.ts.map