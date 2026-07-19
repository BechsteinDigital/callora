import type { AdminContext } from '@/core/auth/adminContext'
import { hasPermission } from '@/core/auth/permissions'

export interface NavItem {
  readonly label: string
  readonly to: string
  // The permission gating visibility. Absent = always visible. It mirrors the
  // server-side read gate of the target so the nav does not offer a link the API
  // would refuse — hiding is convenience, NOT a security boundary (the server
  // stays authoritative, ADR-014 §3.4).
  readonly permission?: string
}

// The admin sidebar model. Array order is display order.
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Übersicht', to: '/' },
  { label: 'Benutzer', to: '/users', permission: 'user.read' },
  { label: 'Rollen', to: '/roles', permission: 'role.read' },
  { label: 'Workspaces', to: '/workspaces', permission: 'workspace.read' },
  { label: 'Mandanten', to: '/tenants', permission: 'tenant.read' },
  { label: 'Plugins', to: '/plugins', permission: 'plugin.read' },
  { label: 'Berechtigungen', to: '/entitlements', permission: 'plugin.read' },
  { label: 'Medien', to: '/media', permission: 'media.read' },
  { label: 'Flows', to: '/flows', permission: 'flow.read' },
  { label: 'Jobs', to: '/jobs', permission: 'job.read' },
  { label: 'Webhooks', to: '/webhooks', permission: 'webhook.read' },
  { label: 'Konfiguration', to: '/config', permission: 'config.read' },
]

// Filters the nav model down to what the current context may see. A super admin
// (the "*" wildcard) sees everything; a scoped admin sees only their read gates.
export function visibleNavItems(ctx: AdminContext | null): NavItem[] {
  return NAV_ITEMS.filter((item) => !item.permission || hasPermission(ctx, item.permission))
}
