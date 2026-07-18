import type { AdminContext } from '@/core/auth/adminContext'

// Mirrors the server-side RequirePermission gate for UI affordances ONLY — the
// server stays authoritative (hiding a button is not a security boundary). The
// super-admin role carries the "*" wildcard permission (InMemoryBackendRbacStore),
// so it satisfies every check; a workspace admin carries the concrete keys.
export function hasPermission(ctx: AdminContext | null, permission: string): boolean {
  if (!ctx) {
    return false
  }
  return ctx.permissions.includes('*') || ctx.permissions.includes(permission)
}
