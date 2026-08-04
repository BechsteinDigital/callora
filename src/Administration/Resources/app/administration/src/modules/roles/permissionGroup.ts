import type { Permission } from './rolesApi'

/** All actions of one function, as the permission matrix groups them. */
export interface PermissionGroup {
  /** The subsystem the actions belong to, e.g. "user" or "plugin". */
  readonly function: string
  readonly permissions: readonly Permission[]
}
