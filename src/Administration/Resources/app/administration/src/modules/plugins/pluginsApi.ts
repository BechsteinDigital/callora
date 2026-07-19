import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors PluginInstallationSnapshot (Core). State is the PluginInstallationState
// enum: 0 Installed, 1 Active, 2 Inactive, 3 Uninstalled.
export interface PluginInstallation {
  pluginId: string
  displayName: string
  assemblyPath: string
  entryTypeName: string | null
  state: number
  installedAtUtc: string
  updatedAtUtc: string
}

export const PluginState = { Installed: 0, Active: 1, Inactive: 2, Uninstalled: 3 } as const

export function isPluginActive(state: number): boolean {
  return state === PluginState.Active
}

// Mirrors PluginLifecycleApiResponse. A lifecycle call returns 200 on success and
// 400/403 on a business failure — always with this body, not an RFC 9457 problem.
export interface PluginLifecycleResult {
  isSuccess: boolean
  pluginId: string | null
  message: string | null
  errorCode: string | null
  warningMessage: string | null
  warningCode: string | null
}

const basePath = '/api/plugins'

// Lifecycle endpoints carry the failure reason in the response body (message/
// errorCode), so unwrap() — which reads RFC 9457 problems — is not enough here.
async function lifecycle(res: Response): Promise<PluginLifecycleResult> {
  const body = (await res.json().catch(() => null)) as PluginLifecycleResult | null
  if (!body) {
    throw new Error(`HTTP ${res.status}`)
  }
  if (!body.isSuccess) {
    throw new Error(body.message ?? body.errorCode ?? `HTTP ${res.status}`)
  }
  return body
}

// Mirrors PluginSignatureStatusApiResponse. State ∈ signed-trusted | unsigned |
// untrusted | revoked | content-hash-mismatch | invalid.
export interface PluginSignatureStatus {
  pluginId: string
  state: string
  signerFingerprint: string | null
}

export const pluginsApi = {
  // Reconciles the registry against the plugin directories first (server-side, for
  // callers with plugin.create), then returns the current installations.
  async list(): Promise<PluginInstallation[]> {
    return (await unwrap(await apiFetch(`${basePath}/installed`))).json()
  },

  // Re-verifies each installed plugin and returns its current signature state.
  async signatureReport(): Promise<PluginSignatureStatus[]> {
    return (await unwrap(await apiFetch(`${basePath}/signature-report`))).json()
  },

  async activate(pluginId: string): Promise<PluginLifecycleResult> {
    return lifecycle(await apiFetch(`${basePath}/${encodeURIComponent(pluginId)}/activate`, jsonInit('POST', {})))
  },

  async deactivate(pluginId: string): Promise<PluginLifecycleResult> {
    return lifecycle(await apiFetch(`${basePath}/${encodeURIComponent(pluginId)}/deactivate`, jsonInit('POST', {})))
  },

  async installLocal(pluginId: string, buildIfNeeded = true): Promise<PluginLifecycleResult> {
    return lifecycle(await apiFetch(`${basePath}/install/local`, jsonInit('POST', { pluginId, buildIfNeeded })))
  },

  async uninstall(pluginId: string): Promise<PluginLifecycleResult> {
    return lifecycle(await apiFetch(`${basePath}/${encodeURIComponent(pluginId)}`, { method: 'DELETE' }))
  },
}
