import type { WorkspacePluginAssignment } from './workspacesApi'

/**
 * What the four backend flags mean together for one workspace.
 *
 * The API reports three independent decisions — the plugin is globally active,
 * the workspace is entitled to it, and it is activated for that workspace —
 * plus `isAssigned`, which is simply entitled AND active. Showing only
 * `isAssigned` hides the states in between: an entitlement revoked on the
 * Berechtigungen page leaves the plugin activated but unusable, and the row
 * would still read "nicht zugewiesen" as if nothing were wrong.
 */
export type AssignmentHealth = 'assigned' | 'unassigned' | 'partial' | 'blocked'

export interface AssignmentStatus {
  readonly health: AssignmentHealth
  readonly label: string
  readonly tone: 'success' | 'neutral' | 'warning'
  /** One sentence naming the situation and, where there is one, the way out. */
  readonly detail: string
}

export function describeAssignment(plugin: WorkspacePluginAssignment): AssignmentStatus {
  // A globally inactive plugin does not run anywhere — that outranks whatever
  // this workspace has configured, in both directions.
  if (!plugin.isGloballyActive) {
    return {
      health: 'blocked',
      label: 'Global inaktiv',
      tone: 'warning',
      detail:
        plugin.isEntitled || plugin.isActive
          ? 'Für diesen Workspace eingerichtet, läuft aber nicht: das Plugin ist global deaktiviert.'
          : 'Das Plugin muss zuerst unter „Plugins“ global aktiviert werden.',
    }
  }

  if (plugin.isEntitled && plugin.isActive) {
    return {
      health: 'assigned',
      label: 'Zugewiesen',
      tone: 'success',
      detail: 'Berechtigt und für diesen Workspace aktiviert.',
    }
  }

  if (!plugin.isEntitled && !plugin.isActive) {
    return {
      health: 'unassigned',
      label: 'Nicht zugewiesen',
      tone: 'neutral',
      detail: 'In diesem Workspace nicht verfügbar.',
    }
  }

  // Exactly one half is set — the case the old single flag swallowed.
  return {
    health: 'partial',
    label: 'Unvollständig',
    tone: 'warning',
    detail: plugin.isEntitled
      ? 'Berechtigt, aber für diesen Workspace nicht aktiviert.'
      : 'Aktiviert, aber die Berechtigung fehlt — erneutes Zuweisen stellt beides her.',
  }
}
