import { apiFetch } from '@/core/http'

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

// Surfaces the RFC 9457 problem detail as an Error message; the caller shows it.
async function unwrap(res: Response): Promise<Response> {
  if (res.ok) {
    return res
  }
  const problem = (await res.json().catch(() => null)) as { detail?: string; title?: string } | null
  throw new Error(problem?.detail ?? problem?.title ?? `HTTP ${res.status}`)
}

function jsonInit(method: string, body: unknown): RequestInit {
  return { method, headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }
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
