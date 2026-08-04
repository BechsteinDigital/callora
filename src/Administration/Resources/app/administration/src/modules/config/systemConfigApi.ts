import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors SystemConfigDefinitionSnapshot (Core). Definitions come from plugin
// config schemas; DefaultValueJson/OptionsJson carry raw JSON text.
export interface ConfigDefinition {
  pluginId: string
  version: string
  configKey: string
  label: string
  fieldType: string
  description: string | null
  defaultValueJson: string | null
  groupName: string | null
  optionsJson: string | null
  sortOrder: number
  isActive: boolean
}

// The /effective response: each value is raw JSON text; secrets read back as
// the JSON string "***" (never the plaintext). The scope is echoed back so a
// caller can tell which view the values belong to.
export interface EffectiveConfig {
  pluginId: string
  tenantKey: string | null
  workspaceKey: string | null
  valuesByKey: Record<string, string>
}

/** Which scope a read or write addresses. A global scope carries no key. */
export interface ConfigScopeSelection {
  scope: string
  scopeKey: string | null
}

export const SECRET_FIELD_TYPE = 'secret'
export const ConfigScope = { Global: 'global', Tenant: 'tenant', Workspace: 'workspace' } as const

export function isSecretField(fieldType: string): boolean {
  return fieldType.trim().toLowerCase() === SECRET_FIELD_TYPE
}

const basePath = '/api/config'

export const systemConfigApi = {
  // Without a pluginId the backend returns every plugin's definitions.
  async listDefinitions(pluginId?: string): Promise<ConfigDefinition[]> {
    const suffix = pluginId ? `?pluginId=${encodeURIComponent(pluginId)}` : ''
    return (await unwrap(await apiFetch(`${basePath}/definitions${suffix}`))).json()
  },

  // Resolved values (workspace > tenant > global > default). Passing a scope
  // narrows the view: omit both keys for the global/default view, pass tenantKey
  // for what a tenant inherits, and workspaceKey for one workspace's effective
  // values. Reading a tenant view is operator-only on the server.
  async effective(pluginId: string, scope: { tenantKey?: string; workspaceKey?: string } = {}): Promise<EffectiveConfig> {
    const params = new URLSearchParams({ pluginId })
    if (scope.tenantKey) {
      params.set('tenantKey', scope.tenantKey)
    }
    if (scope.workspaceKey) {
      params.set('workspaceKey', scope.workspaceKey)
    }
    return (await unwrap(await apiFetch(`${basePath}/effective?${params.toString()}`))).json()
  },

  // Per-key merge on the backend: keys present are upserted at the scope, a null
  // value deletes that key (falling back to the next scope); omitted keys are
  // left untouched.
  async saveValues(
    pluginId: string,
    scope: string,
    scopeKey: string | null,
    valuesByKey: Record<string, unknown>,
  ): Promise<void> {
    await unwrap(await apiFetch(`${basePath}/values`, jsonInit('PUT', { pluginId, scope, scopeKey, valuesByKey })))
  },
}
