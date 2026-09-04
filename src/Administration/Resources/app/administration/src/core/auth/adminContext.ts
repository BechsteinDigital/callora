export interface AdminContext {
  userId: string
  displayName: string | null
  email: string | null
  roles: string[]
  permissions: string[]
  scope: string | null
  workspaceKey: string | null
  /** The tenant a tenant-scoped session is bound to; null for every other scope. */
  tenantKey: string | null
  isOperator: boolean
}

export function parseAdminContext(raw: {
  userId: string
  displayName?: string | null
  email?: string | null
  roles?: string[]
  permissions?: string[]
  scope?: string | null
  workspaceKey?: string | null
  tenantKey?: string | null
  isOperator?: boolean
}): AdminContext {
  return {
    userId: raw.userId,
    displayName: raw.displayName ?? null,
    email: raw.email ?? null,
    roles: raw.roles ?? [],
    permissions: raw.permissions ?? [],
    scope: raw.scope ?? null,
    workspaceKey: raw.workspaceKey ?? null,
    tenantKey: raw.tenantKey ?? null,
    isOperator: raw.isOperator ?? false,
  }
}
