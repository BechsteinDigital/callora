import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors WorkspaceApiResponse (Administration). The tenant key, public host/path
// prefix, theme fields and timestamps are server-derived (read-only in the UI).
export interface Workspace {
  tenantKey: string
  workspaceKey: string
  displayName: string
  workspaceType: string
  isActive: boolean
  tenantIsActive: boolean
  publicBaseUrl: string | null
  publicHost: string | null
  publicPathPrefix: string
  themePluginId: string | null
  themeVersion: string | null
  themeAssignedBy: string | null
  themeAssignedAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

// The mutable slice sent on upsert. The tenant key is server-side (DefaultTenantKey),
// so it is intentionally omitted here.
export interface WorkspaceUpsert {
  displayName: string
  workspaceType: string
  isActive: boolean
  publicBaseUrl: string | null
}

const basePath = '/api/workspaces'

export const workspacesApi = {
  async list(): Promise<Workspace[]> {
    return (await unwrap(await apiFetch(basePath))).json()
  },

  async get(workspaceKey: string): Promise<Workspace> {
    return (await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}`))).json()
  },

  // Create and edit share the PUT upsert route (keyed by workspaceKey).
  async upsert(workspaceKey: string, data: WorkspaceUpsert): Promise<Workspace> {
    return (await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}`, jsonInit('PUT', data)))).json()
  },

  // Cascading purge on the backend (workspace + workspace-bound data, one transaction).
  async remove(workspaceKey: string): Promise<void> {
    await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}`, { method: 'DELETE' }))
  },
}
