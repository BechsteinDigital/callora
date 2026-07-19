import { describe, it, expect } from 'vitest'
import { scopeLabel } from './entitlementsFormat'

describe('scopeLabel', () => {
  it('labels a platform-wide entitlement', () => {
    expect(scopeLabel({ workspaceKey: null, tenantKey: null })).toBe('Plattform')
  })

  it('labels a tenant-scoped entitlement', () => {
    expect(scopeLabel({ workspaceKey: null, tenantKey: 'tenant-a' })).toBe('Tenant: tenant-a')
  })

  it('labels a workspace-scoped entitlement', () => {
    expect(scopeLabel({ workspaceKey: 'workspace-a', tenantKey: null })).toBe('Workspace: workspace-a')
  })

  it('prefers the workspace label when both keys are set (workspace > tenant)', () => {
    expect(scopeLabel({ workspaceKey: 'workspace-a', tenantKey: 'tenant-a' })).toBe('Workspace: workspace-a')
  })
})
