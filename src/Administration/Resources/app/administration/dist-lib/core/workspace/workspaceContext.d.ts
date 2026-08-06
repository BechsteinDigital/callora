import { type Workspace } from '@/modules/workspaces/workspacesApi';
/**
 * The persisted workspace selection, readable before the context is initialised.
 * The bootstrap needs it synchronously: the plugin UI chain is requested before any
 * component mounts, and a platform operator carries no workspace in their token.
 */
export declare function readStoredWorkspace(): string | null;
export declare function useWorkspaceContext(): {
    workspaces: import("vue").Ref<{
        tenantKey: string;
        workspaceKey: string;
        displayName: string;
        workspaceType: string;
        isActive: boolean;
        tenantIsActive: boolean;
        themePluginId: string | null;
        themeVersion: string | null;
        themeAssignedBy: string | null;
        themeAssignedAtUtc: string | null;
        createdAtUtc: string;
        updatedAtUtc: string;
    }[], Workspace[] | {
        tenantKey: string;
        workspaceKey: string;
        displayName: string;
        workspaceType: string;
        isActive: boolean;
        tenantIsActive: boolean;
        themePluginId: string | null;
        themeVersion: string | null;
        themeAssignedBy: string | null;
        themeAssignedAtUtc: string | null;
        createdAtUtc: string;
        updatedAtUtc: string;
    }[]>;
    activeWorkspace: import("vue").ComputedRef<string>;
    fixedWorkspace: import("vue").ComputedRef<string | null>;
    canSwitch: import("vue").ComputedRef<boolean>;
    ensure: () => Promise<void>;
    setActive: (key: string, reload?: () => void) => void;
};
export declare function resetWorkspaceContext(): void;
//# sourceMappingURL=workspaceContext.d.ts.map