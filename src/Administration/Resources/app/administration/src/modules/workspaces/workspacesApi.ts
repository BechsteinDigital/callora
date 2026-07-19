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

// Mirrors WorkspaceMemberApiResponse. The role is a workspace-scoped role name
// (free-form on the backend; only non-empty is enforced).
export interface WorkspaceMember {
  workspaceKey: string
  userId: string
  email: string | null
  displayName: string | null
  role: string
  assignedAtUtc: string
}

// One page of members (PagedApiResponse<WorkspaceMemberApiResponse>). nextCursor is
// null on the last page.
export interface WorkspaceMembersPage {
  items: WorkspaceMember[]
  total: number
  nextCursor: string | null
}

export const MEMBERS_PAGE_SIZE = 50

// Mirrors SurfaceApiResponse (Administration). A surface is a workspace's access/
// output plane (ADR-014 §5). Id, timestamps are server-derived; template/theme are
// carried here so an edit round-trips them (the PUT upsert is a full replace).
export interface WorkspaceSurface {
  id: string
  workspaceKey: string
  surfaceKey: string
  displayName: string
  surfaceType: string
  publicBaseUrl: string | null
  publicHost: string | null
  publicPathPrefix: string
  accessMode: string
  locale: string | null
  templatePluginId: string | null
  templateVersion: string | null
  themePluginId: string | null
  themeVersion: string | null
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

// The mutable slice sent on upsert (the surface key comes from the route). Mirrors
// UpsertSurfaceApiRequest — the full field set, since PUT replaces the surface.
export interface WorkspaceSurfaceUpsert {
  displayName: string
  surfaceType: string
  publicBaseUrl: string | null
  publicHost: string | null
  publicPathPrefix: string
  accessMode: string
  locale: string | null
  templatePluginId: string | null
  templateVersion: string | null
  themePluginId: string | null
  themeVersion: string | null
  isActive: boolean
}

// The backend SurfaceAccessMode enum (ADR-014 §5.2).
export const SURFACE_ACCESS_MODES = ['Public', 'Authenticated', 'Mixed'] as const

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

  // Members are a cursor-paged sub-resource; returns one page. Pass the previous
  // page's nextCursor to fetch the following page.
  async listMembers(workspaceKey: string, cursor?: string): Promise<WorkspaceMembersPage> {
    const params = new URLSearchParams({ limit: String(MEMBERS_PAGE_SIZE) })
    if (cursor) {
      params.set('cursor', cursor)
    }
    return (
      await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}/members?${params.toString()}`))
    ).json()
  },

  // Assign or change a member's workspace role (the user must already exist).
  async upsertMember(workspaceKey: string, userId: string, role: string): Promise<WorkspaceMember> {
    return (
      await unwrap(
        await apiFetch(
          `${basePath}/${encodeURIComponent(workspaceKey)}/members/${encodeURIComponent(userId)}`,
          jsonInit('PUT', { role }),
        ),
      )
    ).json()
  },

  async removeMember(workspaceKey: string, userId: string): Promise<void> {
    await unwrap(
      await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}/members/${encodeURIComponent(userId)}`, {
        method: 'DELETE',
      }),
    )
  },

  // Surfaces are a workspace sub-resource (ADR-014 §5). The list returns full
  // snapshots, so no separate GET-by-key is needed for the admin UI.
  async listSurfaces(workspaceKey: string): Promise<WorkspaceSurface[]> {
    return (await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}/surfaces`))).json()
  },

  // Create and edit share the PUT upsert route (keyed by surfaceKey).
  async upsertSurface(
    workspaceKey: string,
    surfaceKey: string,
    data: WorkspaceSurfaceUpsert,
  ): Promise<WorkspaceSurface> {
    return (
      await unwrap(
        await apiFetch(
          `${basePath}/${encodeURIComponent(workspaceKey)}/surfaces/${encodeURIComponent(surfaceKey)}`,
          jsonInit('PUT', data),
        ),
      )
    ).json()
  },

  async removeSurface(workspaceKey: string, surfaceKey: string): Promise<void> {
    await unwrap(
      await apiFetch(`${basePath}/${encodeURIComponent(workspaceKey)}/surfaces/${encodeURIComponent(surfaceKey)}`, {
        method: 'DELETE',
      }),
    )
  },
}
