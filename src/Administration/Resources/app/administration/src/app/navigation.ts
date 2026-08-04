import {
  Boxes,
  Building2,
  Image,
  KeyRound,
  LayoutDashboard,
  Palette,
  Puzzle,
  Settings,
  ShieldCheck,
  Timer,
  Users,
  Webhook,
  Workflow,
} from 'lucide-vue-next'
import type { AdminContext } from '@/core/auth/adminContext'
import { hasPermission } from '@/core/auth/permissions'
import type { NavGroup, NavGroupId, NavItem } from './navGroup'

// The sidebar is grouped so it stays scannable as subsystems accumulate: an
// operator looks for a *kind* of thing first ("something about access") and only
// then for the exact page.
const GROUP_LABELS: Record<NavGroupId, string | null> = {
  // The dashboard stands alone, above the first heading.
  overview: null,
  management: 'Verwaltung',
  content: 'Inhalte',
  system: 'System',
}

const GROUP_ORDER: readonly NavGroupId[] = ['overview', 'management', 'content', 'system']

/**
 * The admin sidebar model. Array order is display order within a group.
 *
 * `permission` mirrors the server-side read gate of the target so the nav does
 * not offer a link the API would refuse — hiding is convenience, NOT a security
 * boundary (the server stays authoritative, ADR-014 §3.4).
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Übersicht', to: '/', icon: LayoutDashboard, group: 'overview' },

  { label: 'Benutzer', to: '/users', icon: Users, permission: 'user.read', group: 'management' },
  { label: 'Rollen', to: '/roles', icon: ShieldCheck, permission: 'role.read', group: 'management' },
  { label: 'Workspaces', to: '/workspaces', icon: Boxes, permission: 'workspace.read', group: 'management' },
  { label: 'Mandanten', to: '/tenants', icon: Building2, permission: 'tenant.read', group: 'management' },

  { label: 'Medien', to: '/media', icon: Image, permission: 'media.read', group: 'content' },
  { label: 'Themes', to: '/themes', icon: Palette, permission: 'extension.read', group: 'content' },
  { label: 'Flows', to: '/flows', icon: Workflow, permission: 'flow.read', group: 'content' },

  { label: 'Plugins', to: '/plugins', icon: Puzzle, permission: 'plugin.read', group: 'system' },
  { label: 'Berechtigungen', to: '/entitlements', icon: KeyRound, permission: 'plugin.read', group: 'system' },
  { label: 'Jobs', to: '/jobs', icon: Timer, permission: 'job.read', group: 'system' },
  { label: 'Webhooks', to: '/webhooks', icon: Webhook, permission: 'webhook.read', group: 'system' },
  { label: 'Konfiguration', to: '/config', icon: Settings, permission: 'config.read', group: 'system' },
]

/**
 * Filters the nav model down to what the current context may see. A super admin
 * (the "*" wildcard) sees everything; a scoped admin sees only their read gates.
 */
export function visibleNavItems(ctx: AdminContext | null): NavItem[] {
  return NAV_ITEMS.filter((item) => !item.permission || hasPermission(ctx, item.permission))
}

/**
 * The same visible items, arranged into the sidebar's groups. Empty groups are
 * dropped, so a narrowly-scoped admin never sees a heading with nothing under it.
 */
export function visibleNavGroups(ctx: AdminContext | null): NavGroup[] {
  const visible = visibleNavItems(ctx)
  return GROUP_ORDER.map((id) => ({
    id,
    label: GROUP_LABELS[id],
    items: visible.filter((item) => item.group === id),
  })).filter((group) => group.items.length > 0)
}
