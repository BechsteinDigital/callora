import { describe, it, expect } from 'vitest'
import { parseAdminContext } from './adminContext'

describe('parseAdminContext', () => {
  it('maps the API shape to the store model', () => {
    const ctx = parseAdminContext({
      userId: 'u1',
      displayName: 'Max',
      email: 'max@x.de',
      roles: ['workspace-admin'],
      permissions: ['workspace.read'],
      scope: 'workspace',
      workspaceKey: 'sales-de',
      isOperator: false,
    })
    expect(ctx.userId).toBe('u1')
    expect(ctx.isOperator).toBe(false)
    expect(ctx.permissions).toContain('workspace.read')
    expect(ctx.workspaceKey).toBe('sales-de')
  })

  it('defaults arrays and nullables when absent', () => {
    const ctx = parseAdminContext({ userId: 'u2', isOperator: true })
    expect(ctx.roles).toEqual([])
    expect(ctx.permissions).toEqual([])
    expect(ctx.displayName).toBeNull()
    expect(ctx.workspaceKey).toBeNull()
  })
})
