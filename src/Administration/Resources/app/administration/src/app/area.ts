import type { AdminContext } from '@/core/auth/adminContext'

/**
 * The three levels an admin session can be in. Not a UI preference — the area is
 * what the session IS, carried in the token as `callora_scope`.
 *
 * The instance operator runs the host; the tenant is the customer, who runs their
 * own workspaces; a workspace is where the work happens. Most people are exactly
 * one of them: an agency signs in as platform, its customer as tenant, that
 * customer's staff as workspace.
 */
export type AreaId = 'platform' | 'tenant' | 'workspace'

export const AREA_ORDER: readonly AreaId[] = ['platform', 'tenant', 'workspace']

/** The user-facing name. The code keeps its terms, the UI gets the dictionary's. */
export const AREA_LABELS: Record<AreaId, string> = {
  platform: 'Plattform',
  tenant: 'Mandant',
  workspace: 'Workspace',
}

/**
 * The area of the current session, or null when there is none.
 *
 * Derived from the scope claim rather than from the permissions: a permission set
 * can look tenant-ish by accident, a scope cannot. `isOperator` wins because an
 * operator is never down-scoped at login — the same rule AdminLoginResolver follows.
 */
export function currentArea(ctx: AdminContext | null): AreaId | null {
  if (!ctx) return null
  if (ctx.isOperator || ctx.scope === 'platform') return 'platform'
  if (ctx.scope === 'tenant') return 'tenant'
  if (ctx.scope === 'workspace') return 'workspace'
  return null
}

/**
 * What the area heading says underneath its name: which tenant, which workspace.
 *
 * "Mandant" without the tenant's name is a heading that tells nobody where they
 * are — and the whole point of the level is that there is more than one of them.
 */
export function currentAreaSubject(ctx: AdminContext | null): string | null {
  switch (currentArea(ctx)) {
    case 'tenant':
      return ctx?.tenantKey ?? null
    case 'workspace':
      return ctx?.workspaceKey ?? null
    default:
      return null
  }
}
