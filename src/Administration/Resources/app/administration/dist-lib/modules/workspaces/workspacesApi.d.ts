export interface Workspace {
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
}
export interface WorkspaceUpsert {
    displayName: string;
    workspaceType: string;
    isActive: boolean;
    defaultSurfaceBaseUrl: string | null;
}
export interface WorkspaceMember {
    workspaceKey: string;
    userId: string;
    email: string | null;
    displayName: string | null;
    role: string;
    assignedAtUtc: string;
}
export interface WorkspaceMembersPage {
    items: WorkspaceMember[];
    total: number;
    nextCursor: string | null;
}
export declare const MEMBERS_PAGE_SIZE = 50;
export interface WorkspaceSurface {
    id: string;
    workspaceKey: string;
    surfaceKey: string;
    displayName: string;
    surfaceType: string;
    publicBaseUrl: string | null;
    publicHost: string | null;
    publicPathPrefix: string;
    accessMode: string;
    locale: string | null;
    templatePluginId: string | null;
    templateVersion: string | null;
    themePluginId: string | null;
    themeVersion: string | null;
    isActive: boolean;
    createdAtUtc: string;
    updatedAtUtc: string;
}
export interface WorkspaceSurfaceUpsert {
    displayName: string;
    surfaceType: string;
    publicBaseUrl: string | null;
    publicHost: string | null;
    publicPathPrefix: string;
    accessMode: string;
    locale: string | null;
    templatePluginId: string | null;
    templateVersion: string | null;
    themePluginId: string | null;
    themeVersion: string | null;
    isActive: boolean;
}
export interface WorkspacePluginAssignment {
    pluginId: string;
    displayName: string;
    isGloballyActive: boolean;
    isEntitled: boolean;
    isActive: boolean;
    isAssigned: boolean;
}
export declare const SURFACE_ACCESS_MODES: readonly ["Public", "Authenticated", "Mixed"];
export declare const workspacesApi: {
    list(): Promise<Workspace[]>;
    get(workspaceKey: string): Promise<Workspace>;
    upsert(workspaceKey: string, data: WorkspaceUpsert): Promise<Workspace>;
    remove(workspaceKey: string): Promise<void>;
    listMembers(workspaceKey: string, cursor?: string): Promise<WorkspaceMembersPage>;
    upsertMember(workspaceKey: string, userId: string, role: string): Promise<WorkspaceMember>;
    removeMember(workspaceKey: string, userId: string): Promise<void>;
    listSurfaces(workspaceKey: string): Promise<WorkspaceSurface[]>;
    upsertSurface(workspaceKey: string, surfaceKey: string, data: WorkspaceSurfaceUpsert): Promise<WorkspaceSurface>;
    removeSurface(workspaceKey: string, surfaceKey: string): Promise<void>;
    listPlugins(workspaceKey: string): Promise<WorkspacePluginAssignment[]>;
    setPluginAssignment(workspaceKey: string, pluginId: string, isAssigned: boolean): Promise<WorkspacePluginAssignment>;
};
//# sourceMappingURL=workspacesApi.d.ts.map