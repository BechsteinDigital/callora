export interface AdminWorkspace {
  tenantKey: string;
  workspaceKey: string;
  displayName: string;
  workspaceType: string;
  isActive: boolean;
  tenantIsActive: boolean;
  publicBaseUrl: string | null;
  publicHost: string | null;
  publicPathPrefix: string;
  themePluginId: string | null;
  themeVersion: string | null;
  themeAssignedBy: string | null;
  themeAssignedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpsertAdminWorkspaceRequest {
  tenantKey?: string;
  displayName: string;
  workspaceType: string;
  isActive: boolean;
  publicBaseUrl?: string | null;
}

export interface AdminTenant {
  tenantKey: string;
  displayName: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}
