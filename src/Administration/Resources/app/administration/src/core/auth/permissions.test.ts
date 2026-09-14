import { describe, it, expect } from 'vitest'
import { hasPermission } from './permissions'
import type { AdminContext } from './adminContext'

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: null,
    tenantKey: null,
    isOperator: false,
  }
}

describe('hasPermission', () => {
  it('grants when the exact permission is present', () => {
    expect(hasPermission(ctx(['user.read']), 'user.read')).toBe(true)
  })

  it('denies when the permission is absent', () => {
    expect(hasPermission(ctx(['user.read']), 'user.create')).toBe(false)
  })

  it('grants everything for the "*" wildcard (super admin)', () => {
    expect(hasPermission(ctx(['*']), 'role.update')).toBe(true)
  })

  it('denies for a null context', () => {
    expect(hasPermission(null, 'user.read')).toBe(false)
  })
})
