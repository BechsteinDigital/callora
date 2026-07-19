// Pure presentation helper for the entitlement view, kept separate for unit tests.

// Derives a human scope label from which keys are set — workspace > tenant >
// platform (mirrors the backend resolution precedence).
export function scopeLabel(entitlement: { workspaceKey: string | null; tenantKey: string | null }): string {
  if (entitlement.workspaceKey) {
    return `Workspace: ${entitlement.workspaceKey}`
  }
  if (entitlement.tenantKey) {
    return `Tenant: ${entitlement.tenantKey}`
  }
  return 'Plattform'
}
