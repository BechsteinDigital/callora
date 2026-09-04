import {
  Boxes,
  Building2,
  Image,
  KeyRound,
  Layers,
  LayoutDashboard,
  Palette,
  Puzzle,
  Settings,
  ShieldCheck,
  Timer,
  Type,
  Users,
  Webhook,
  Workflow,
} from 'lucide-vue-next'
import type { AdminContext } from '@/core/auth/adminContext'
import { hasPermission } from '@/core/auth/permissions'
import type { AreaId } from './area'
import { AREA_ORDER, currentArea } from './area'
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
  { label: 'Übersicht', to: '/', icon: LayoutDashboard, group: 'overview', areas: ['platform', 'tenant', 'workspace'] },

  { label: 'Benutzer', to: '/users', icon: Users, permission: 'user.read', group: 'management', areas: ['platform'] },
  { label: 'Rollen', to: '/roles', icon: ShieldCheck, permission: 'role.read', group: 'management', areas: ['platform'] },
  { label: 'Workspaces', to: '/workspaces', icon: Boxes, permission: 'workspace.read', group: 'management', areas: ['platform', 'tenant'] },
  { label: 'Mandanten', to: '/tenants', icon: Building2, permission: 'tenant.read', group: 'management', areas: ['platform'] },

  { label: 'Flächen', to: '/surfaces', icon: Layers, permission: 'workspace.read', group: 'content', areas: ['workspace'] },
  { label: 'Medien', to: '/media', icon: Image, permission: 'media.read', group: 'content', areas: ['workspace'] },
  { label: 'Themes', to: '/themes', icon: Palette, permission: 'extension.read', group: 'content', areas: ['workspace'] },
  { label: 'Flows', to: '/flows', icon: Workflow, permission: 'flow.read', group: 'content', areas: ['workspace'] },

  { label: 'Plugins', to: '/plugins', icon: Puzzle, permission: 'plugin.read', group: 'system', areas: ['platform'] },
  { label: 'Berechtigungen', to: '/entitlements', icon: KeyRound, permission: 'plugin.read', group: 'system', areas: ['platform', 'tenant'] },
  { label: 'Jobs', to: '/jobs', icon: Timer, permission: 'job.read', group: 'system', areas: ['platform', 'workspace'] },
  { label: 'Webhooks', to: '/webhooks', icon: Webhook, permission: 'webhook.read', group: 'system', areas: ['workspace'] },
  { label: 'Konfiguration', to: '/config', icon: Settings, permission: 'config.read', group: 'system', areas: ['platform', 'workspace'] },
  { label: 'Texte', to: '/snippets', icon: Type, permission: 'snippet.read', group: 'system', areas: ['platform'] },
]

/**
 * Filters the nav model down to what the current context may see. A super admin
 * (the "*" wildcard) sees everything; a scoped admin sees only their read gates.
 */
export function visibleNavItems(ctx: AdminContext | null, area?: AreaId | null): NavItem[] {
  const effective = area ?? currentArea(ctx)
  return NAV_ITEMS.filter(
    (item) =>
      (!item.permission || hasPermission(ctx, item.permission)) &&
      // Ohne Bereich bleibt es beim reinen Rechtefilter: Das ist der Zustand vor der
      // Anmeldung und der eines Aufrufers, dessen Sitzung keinen Scope trägt — dort etwas
      // auszublenden hieße raten.
      (effective === null || item.areas.includes(effective)),
  )
}

/**
 * The areas this session can look at.
 *
 * An operator reaches all three: they run the host, and the topbar's workspace picker
 * already lets them say WHICH workspace they are looking at. Everyone else sees the one
 * area their session is — moving to another means a new session
 * (`POST /api/auth/scope`), not a click.
 */
export function availableAreas(ctx: AdminContext | null): AreaId[] {
  if (!ctx) return []
  if (ctx.isOperator) return [...AREA_ORDER]
  const area = currentArea(ctx)
  return area ? [area] : []
}

/**
 * The same visible items, arranged into the sidebar's groups. Empty groups are
 * dropped, so a narrowly-scoped admin never sees a heading with nothing under it.
 */
export function visibleNavGroups(ctx: AdminContext | null, area?: AreaId | null): NavGroup[] {
  const visible = visibleNavItems(ctx, area)
  return GROUP_ORDER.map((id) => ({
    id,
    label: GROUP_LABELS[id],
    items: visible.filter((item) => item.group === id),
  })).filter((group) => group.items.length > 0)
}
