export interface ServiceRegistrationMeta {
    readonly pluginId?: string | null;
    readonly priority?: number;
}
export declare function registerService<T>(key: string, implementation: T, meta?: ServiceRegistrationMeta): void;
export declare function useService<T>(key: string, fallback: T): T;
export interface ServiceConflict {
    readonly key: string;
    readonly activePluginId: string | null;
    readonly shadowedPluginIds: (string | null)[];
}
export declare function getServiceConflicts(): ServiceConflict[];
export declare function resetServices(): void;
//# sourceMappingURL=services.d.ts.map