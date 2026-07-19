import { apiFetch, jsonInit, unwrap } from '@/core/http'

export interface Tenant {
  tenantKey: string
  displayName: string
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

const tenantsPath = '/api/tenants'

export const tenantsApi = {
  async list(): Promise<Tenant[]> {
    return (await unwrap(await apiFetch(tenantsPath))).json()
  },

  async create(tenantKey: string, displayName: string): Promise<Tenant> {
    return (await unwrap(await apiFetch(tenantsPath, jsonInit('POST', { tenantKey, displayName })))).json()
  },

  async activate(tenantKey: string): Promise<void> {
    await unwrap(await apiFetch(`${tenantsPath}/${encodeURIComponent(tenantKey)}/activate`, { method: 'POST' }))
  },

  async suspend(tenantKey: string): Promise<void> {
    await unwrap(await apiFetch(`${tenantsPath}/${encodeURIComponent(tenantKey)}/suspend`, { method: 'POST' }))
  },

  async remove(tenantKey: string): Promise<void> {
    await unwrap(await apiFetch(`${tenantsPath}/${encodeURIComponent(tenantKey)}`, { method: 'DELETE' }))
  },
}
