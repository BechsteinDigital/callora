export interface HookContext<T> {
    readonly payload: T;
    cancel(reason?: string): void;
}
export interface HookOutcome {
    readonly canceled: boolean;
    readonly cancelReason?: string;
}
type HookHandler<T> = (ctx: HookContext<T>) => void | Promise<void>;
export declare function registerHook<T>(name: string, handler: HookHandler<T>, order?: number, pluginId?: string | null): void;
export declare function runHook<T>(name: string, payload: T): Promise<HookOutcome>;
export declare function resetHooks(): void;
export {};
//# sourceMappingURL=hooks.d.ts.map