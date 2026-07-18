import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Shapes mirror the backend responses (camelCase JSON).
export interface BackendUser {
  externalId: string
  email: string | null
  displayName: string | null
  hasPassword: boolean
  passwordHashAlgorithm: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface Role {
  role: string
  permissions: string[]
}

export interface CreateUserInput {
  externalId: string
  email: string | null
  displayName: string | null
  password: string
}

export interface UpdateUserInput {
  email: string | null
  displayName: string | null
  // null leaves the stored password untouched (backend contract).
  password: string | null
}

const usersPath = '/api/users'
const rbacUsersPath = '/api/security/rbac/users'
const rbacRolesPath = '/api/security/rbac/roles'

export const usersApi = {
  async list(): Promise<BackendUser[]> {
    return (await unwrap(await apiFetch(usersPath))).json()
  },

  async get(userId: string): Promise<BackendUser> {
    return (await unwrap(await apiFetch(`${usersPath}/${encodeURIComponent(userId)}`))).json()
  },

  async create(input: CreateUserInput): Promise<BackendUser> {
    return (await unwrap(await apiFetch(usersPath, jsonInit('POST', input)))).json()
  },

  async update(userId: string, input: UpdateUserInput): Promise<BackendUser> {
    return (await unwrap(await apiFetch(`${usersPath}/${encodeURIComponent(userId)}`, jsonInit('PUT', input)))).json()
  },

  async remove(userId: string): Promise<void> {
    await unwrap(await apiFetch(`${usersPath}/${encodeURIComponent(userId)}`, { method: 'DELETE' }))
  },

  async listRoles(): Promise<Role[]> {
    return (await unwrap(await apiFetch(rbacRolesPath))).json()
  },

  // Flattens the [{ userId, role }] assignments into a userId → role lookup.
  async listRoleAssignments(): Promise<Record<string, string>> {
    const assignments = (await (await unwrap(await apiFetch(rbacUsersPath))).json()) as {
      userId: string
      role: string
    }[]
    return Object.fromEntries(assignments.map((a) => [a.userId, a.role]))
  },

  async assignRole(userId: string, role: string): Promise<void> {
    await unwrap(await apiFetch(`${rbacUsersPath}/${encodeURIComponent(userId)}`, jsonInit('PUT', { role })))
  },
}
