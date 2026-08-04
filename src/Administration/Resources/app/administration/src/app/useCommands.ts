import { computed, type ComputedRef } from 'vue'
import { LogOut, Moon, Plus, Sun } from 'lucide-vue-next'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import { useTheme } from '@/core/design/theme'
import { usePluginNavigation } from '@/core/extensions/pluginNavigation'
import { resolvePluginIcon } from '@/core/extensions/pluginIcons'
import { visibleNavGroups } from './navigation'
import type { CommandItem } from './commandItem'

/**
 * The command palette's catalogue: every page the operator may open, the
 * create-actions behind their write permissions, and the shell's own switches.
 *
 * It is derived, not maintained by hand — a nav entry or an installed plugin
 * shows up in the palette automatically, and permission gating is the same
 * filter the sidebar uses.
 */
export function useCommands(onLogout: () => void): ComputedRef<CommandItem[]> {
  const ctx = useAuthStore().context
  const { items: pluginNav } = usePluginNavigation()
  const { resolved, toggle } = useTheme()

  return computed<CommandItem[]>(() => {
    const commands: CommandItem[] = []

    for (const group of visibleNavGroups(ctx.value)) {
      for (const item of group.items) {
        commands.push({
          id: `nav:${item.to}`,
          label: item.label,
          section: group.label ?? 'Übersicht',
          icon: item.icon,
          to: item.to,
          keywords: NAV_KEYWORDS[item.to],
        })
      }
    }

    for (const item of pluginNav.value) {
      commands.push({
        id: `plugin:${item.pluginId}:${item.id}`,
        label: item.label,
        section: 'Erweiterungen',
        icon: resolvePluginIcon(item.icon),
        to: item.to,
        keywords: [item.pluginId],
      })
    }

    // Create-actions are gated on the write permission, not the read gate that
    // governs the corresponding page.
    for (const action of CREATE_ACTIONS) {
      if (hasPermission(ctx.value, action.permission)) {
        commands.push({
          id: `action:${action.to}`,
          label: action.label,
          section: 'Aktionen',
          icon: Plus,
          to: action.to,
          keywords: action.keywords,
        })
      }
    }

    commands.push({
      id: 'action:theme',
      label: resolved.value === 'dark' ? 'Helles Design' : 'Dunkles Design',
      section: 'Aktionen',
      icon: resolved.value === 'dark' ? Sun : Moon,
      run: toggle,
      keywords: ['theme', 'design', 'dark', 'light', 'hell', 'dunkel'],
    })

    commands.push({
      id: 'action:logout',
      label: 'Abmelden',
      section: 'Aktionen',
      icon: LogOut,
      run: onLogout,
      keywords: ['logout', 'sign out', 'abmelden', 'ausloggen'],
    })

    return commands
  })
}

// English and colloquial terms an operator is likely to type for a German label.
const NAV_KEYWORDS: Record<string, readonly string[]> = {
  '/': ['dashboard', 'home', 'start'],
  '/users': ['users', 'accounts', 'konten'],
  '/roles': ['roles', 'rbac', 'rechte', 'permissions'],
  '/workspaces': ['workspaces', 'arbeitsbereiche'],
  '/tenants': ['tenants', 'kunden', 'mandanten'],
  '/media': ['media', 'dateien', 'bilder', 'uploads'],
  '/themes': ['themes', 'design', 'branding'],
  '/flows': ['flows', 'automation', 'regeln'],
  '/plugins': ['plugins', 'extensions', 'erweiterungen'],
  '/entitlements': ['entitlements', 'lizenzen', 'features'],
  '/jobs': ['jobs', 'queue', 'aufgaben'],
  '/webhooks': ['webhooks', 'hooks', 'events'],
  '/config': ['config', 'settings', 'einstellungen'],
}

const CREATE_ACTIONS: readonly {
  label: string
  to: string
  permission: string
  keywords: readonly string[]
}[] = [
  { label: 'Benutzer anlegen', to: '/users/new', permission: 'user.create', keywords: ['new user', 'neuer benutzer'] },
  { label: 'Rolle anlegen', to: '/roles/new', permission: 'role.create', keywords: ['new role', 'neue rolle'] },
  {
    label: 'Workspace anlegen',
    to: '/workspaces/new',
    permission: 'workspace.create',
    keywords: ['new workspace', 'neuer workspace'],
  },
]
