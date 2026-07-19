import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors ThemeDefinitionApiResponse — one registered theme (surface bundle,
// token axis) that a workspace can be assigned. ADR-014 §10.
export interface ThemeDefinition {
  templateKey: string
  surface: string
  pluginId: string
  version: string
  displayName: string
  templatePath: string
  parentTemplateKey: string | null
  scope: string
  isActive: boolean
  priority: number
  createdAtUtc: string
  updatedAtUtc: string
}

// Mirrors WorkspaceThemeAssignmentApiResponse. A null themePluginId means no
// theme is assigned (the GET returns 404 in that case → null here).
export interface ThemeAssignment {
  workspaceKey: string
  themePluginId: string | null
  themeVersion: string | null
  assignedBy: string | null
  assignedAtUtc: string | null
}

const basePath = '/api/themes'

export const themesApi = {
  // The pickable themes for a workspace surface (active workspace-scope definitions).
  async listDefinitions(): Promise<ThemeDefinition[]> {
    const params = new URLSearchParams({ surface: 'workspace', active: 'true' })
    return (await unwrap(await apiFetch(`${basePath}/definitions?${params.toString()}`))).json()
  },

  // Returns the current assignment or null when none is set (404).
  async getAssignment(workspaceKey: string): Promise<ThemeAssignment | null> {
    const res = await apiFetch(`${basePath}/workspaces/${encodeURIComponent(workspaceKey)}`)
    if (res.status === 404) {
      return null
    }
    return (await unwrap(res)).json()
  },

  async assign(workspaceKey: string, themePluginId: string, themeVersion: string): Promise<ThemeAssignment> {
    return (
      await unwrap(
        await apiFetch(
          `${basePath}/workspaces/${encodeURIComponent(workspaceKey)}`,
          jsonInit('PUT', { themePluginId, themeVersion, assignedBy: null }),
        ),
      )
    ).json()
  },

  async clearAssignment(workspaceKey: string): Promise<void> {
    await unwrap(
      await apiFetch(`${basePath}/workspaces/${encodeURIComponent(workspaceKey)}`, { method: 'DELETE' }),
    )
  },
}
