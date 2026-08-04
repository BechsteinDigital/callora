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

// Mirrors WorkspaceThemeSettingDefinitionApiResponse — one token/setting field of
// the assigned theme (label, type, default, options), schema-driven like config.
export interface ThemeSettingDefinition {
  settingKey: string
  label: string
  fieldType: string
  description: string | null
  defaultValueJson: string | null
  isRequired: boolean
  sortOrder: number
  groupName: string | null
  optionsJson: string | null
  isActive: boolean
}

// Mirrors WorkspaceThemeSettingsApiResponse. valuesByKey holds the raw JSON string
// per setting key; fields is empty when no theme is assigned.
export interface ThemeSettings {
  workspaceKey: string
  hasAssignedTheme: boolean
  themePluginId: string | null
  themeVersion: string | null
  fields: ThemeSettingDefinition[]
  valuesByKey: Record<string, string>
}

// Mirrors SurfaceThemeAssignmentApiResponse. `inheritedFromWorkspace` is true
// while the surface simply follows its workspace's theme.
export interface SurfaceThemeAssignment {
  workspaceKey: string
  surfaceKey: string
  themePluginId: string | null
  themeVersion: string | null
  inheritedFromWorkspace: boolean
}

// Mirrors SurfaceThemeSettingsApiResponse: the theme's fields, the values this
// surface overrides, and what it would otherwise inherit from its workspace.
export interface SurfaceThemeSettings {
  workspaceKey: string
  surfaceKey: string
  hasAssignedTheme: boolean
  themePluginId: string | null
  themeVersion: string | null
  inheritedFromWorkspace: boolean
  // False when the surface runs a different theme than its workspace — the
  // workspace values then belong to another theme and do not apply.
  inheritsWorkspaceValues: boolean
  fields: ThemeSettingDefinition[]
  valuesByKey: Record<string, string>
  inheritedValuesByKey: Record<string, string>
}

const basePath = '/api/themes'

function surfacePath(workspaceKey: string, surfaceKey: string): string {
  return `${basePath}/workspaces/${encodeURIComponent(workspaceKey)}/surfaces/${encodeURIComponent(surfaceKey)}`
}

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

  async getSettings(workspaceKey: string): Promise<ThemeSettings> {
    return (
      await unwrap(await apiFetch(`${basePath}/workspaces/${encodeURIComponent(workspaceKey)}/settings`))
    ).json()
  },

  // Replaces the workspace's theme setting values; the backend keeps only keys of
  // active definitions. Values are parsed JSON (a JSON null clears a setting).
  async saveSettings(workspaceKey: string, valuesByKey: Record<string, unknown>): Promise<ThemeSettings> {
    return (
      await unwrap(
        await apiFetch(
          `${basePath}/workspaces/${encodeURIComponent(workspaceKey)}/settings`,
          jsonInit('PUT', { valuesByKey }),
        ),
      )
    ).json()
  },

  // ------------------------------------------------------------- Surface level

  async getSurfaceAssignment(workspaceKey: string, surfaceKey: string): Promise<SurfaceThemeAssignment> {
    return (await unwrap(await apiFetch(surfacePath(workspaceKey, surfaceKey)))).json()
  },

  // Pins the surface to its own theme, overriding the workspace.
  async assignSurface(
    workspaceKey: string,
    surfaceKey: string,
    themePluginId: string,
    themeVersion: string,
  ): Promise<SurfaceThemeAssignment> {
    return (
      await unwrap(
        await apiFetch(surfacePath(workspaceKey, surfaceKey), jsonInit('PUT', { themePluginId, themeVersion })),
      )
    ).json()
  },

  // Drops the surface's own theme so it follows the workspace again. Its stored
  // values go with it — they belong to the theme being detached.
  async clearSurfaceAssignment(workspaceKey: string, surfaceKey: string): Promise<SurfaceThemeAssignment> {
    return (
      await unwrap(await apiFetch(surfacePath(workspaceKey, surfaceKey), { method: 'DELETE' }))
    ).json()
  },

  async getSurfaceSettings(workspaceKey: string, surfaceKey: string): Promise<SurfaceThemeSettings> {
    return (await unwrap(await apiFetch(`${surfacePath(workspaceKey, surfaceKey)}/settings`))).json()
  },

  // Replaces the surface's own values. An omitted key falls back to the
  // workspace value, and from there to the theme default.
  async saveSurfaceSettings(
    workspaceKey: string,
    surfaceKey: string,
    valuesByKey: Record<string, unknown>,
  ): Promise<SurfaceThemeSettings> {
    return (
      await unwrap(
        await apiFetch(`${surfacePath(workspaceKey, surfaceKey)}/settings`, jsonInit('PUT', { valuesByKey })),
      )
    ).json()
  },
}
