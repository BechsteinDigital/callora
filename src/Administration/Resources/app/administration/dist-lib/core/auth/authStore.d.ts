import { type AdminContext } from '@/core/auth/adminContext';
declare function loadContext(): Promise<boolean>;
declare function login(loginName: string, password: string, workspaceKey: string | null): Promise<boolean>;
declare function logout(): Promise<void>;
declare function reset(): void;
export declare function useAuthStore(): {
    context: import("vue").Ref<{
        userId: string;
        displayName: string | null;
        email: string | null;
        roles: string[];
        permissions: string[];
        scope: string | null;
        workspaceKey: string | null;
        isOperator: boolean;
    } | null, AdminContext | {
        userId: string;
        displayName: string | null;
        email: string | null;
        roles: string[];
        permissions: string[];
        scope: string | null;
        workspaceKey: string | null;
        isOperator: boolean;
    } | null>;
    login: typeof login;
    logout: typeof logout;
    loadContext: typeof loadContext;
    reset: typeof reset;
};
export {};
//# sourceMappingURL=authStore.d.ts.map