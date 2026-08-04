import { ConfigScope } from './systemConfigApi'
import type { AdminContext } from '@/core/auth/adminContext'

/** One selectable scope in the configuration view. */
export interface ConfigScopeOption {
  readonly value: string
  readonly label: string
  /** What editing at this level means, shown under the picker. */
  readonly description: string
  /** Whether the scope needs a key (tenant/workspace) or stands alone (global). */
  readonly needsKey: boolean
}

const OPTIONS: readonly ConfigScopeOption[] = [
  {
    value: ConfigScope.Global,
    label: 'Plattform',
    description: 'Gilt überall, sofern kein Mandant oder Workspace etwas anderes setzt.',
    needsKey: false,
  },
  {
    value: ConfigScope.Tenant,
    label: 'Mandant',
    description: 'Gilt für alle Workspaces dieses Mandanten und sticht die Plattform-Ebene.',
    needsKey: true,
  },
  {
    value: ConfigScope.Workspace,
    label: 'Workspace',
    description: 'Gilt nur für diesen Workspace und sticht Mandant und Plattform.',
    needsKey: true,
  },
]

/**
 * The scopes a caller may actually address.
 *
 * Mirrors the server's rule: writing (and reading) the global and tenant level
 * is operator-only, while a workspace-bound admin may edit their own workspace.
 * Offering a scope the server would refuse is worse than not offering it.
 */
export function availableScopes(ctx: AdminContext | null): ConfigScopeOption[] {
  if (ctx?.isOperator) {
    return [...OPTIONS]
  }
  return OPTIONS.filter((option) => option.value === ConfigScope.Workspace)
}

export function scopeOption(value: string): ConfigScopeOption | null {
  return OPTIONS.find((option) => option.value === value) ?? null
}
