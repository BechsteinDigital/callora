import { apiFetch, jsonInit, unwrap } from '@/core/http'

export interface Role {
  role: string
  permissions: string[]
}

export interface Permission {
  permissionKey: string
  function: string
  action: string
}

// The super-admin role is system/fixed (wildcard "*"); the backend rejects edits
// and deletes on it, so the UI treats it as read-only.
export const SYSTEM_ROLE = 'superadmin'

const rolesPath = '/api/security/rbac/roles'
const permissionsPath = '/api/security/rbac/permissions'

export const rolesApi = {
  async list(): Promise<Role[]> {
    return (await unwrap(await apiFetch(rolesPath))).json()
  },

  async listPermissions(): Promise<Permission[]> {
    return (await unwrap(await apiFetch(permissionsPath))).json()
  },

  // Groups the flat "function.action" keys back into the { function, actions[] }
  // shape the backend expects (it splits on the first dot).
  async upsert(role: string, permissionKeys: string[]): Promise<void> {
    const byFunction = new Map<string, string[]>()
    for (const key of permissionKeys) {
      const dot = key.indexOf('.')
      if (dot <= 0 || dot === key.length - 1) {
        continue
      }
      const fn = key.slice(0, dot)
      const action = key.slice(dot + 1)
      const actions = byFunction.get(fn) ?? []
      actions.push(action)
      byFunction.set(fn, actions)
    }
    const functions = [...byFunction.entries()].map(([fn, actions]) => ({ function: fn, actions }))
    await unwrap(await apiFetch(`${rolesPath}/${encodeURIComponent(role)}`, jsonInit('PUT', { functions })))
  },

  async remove(role: string): Promise<void> {
    await unwrap(await apiFetch(`${rolesPath}/${encodeURIComponent(role)}`, { method: 'DELETE' }))
  },
}
