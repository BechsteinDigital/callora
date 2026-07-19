import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors EntitlementApiResponse. Scope is workspace > tenant > platform depending
// on which keys are set (both null = platform-wide).
export interface Entitlement {
  pluginId: string
  workspaceKey: string | null
  tenantKey: string | null
  isEntitled: boolean
  source: string
  createdAtUtc: string
  updatedAtUtc: string
}

// Mirrors SetEntitlementApiRequest — grant (isEntitled true) or revoke (false).
export interface SetEntitlementInput {
  pluginId: string
  workspaceKey: string | null
  tenantKey: string | null
  isEntitled: boolean
}

const basePath = '/api/entitlements'

export const entitlementsApi = {
  async list(): Promise<Entitlement[]> {
    return (await unwrap(await apiFetch(basePath))).json()
  },

  // Grant and revoke share the PUT route; the isEntitled flag selects the action.
  async set(input: SetEntitlementInput): Promise<void> {
    await unwrap(await apiFetch(basePath, jsonInit('PUT', input)))
  },
}
