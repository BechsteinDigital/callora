import { describe, it, expect } from 'vitest'
import { NAV_ITEMS, visibleNavItems } from './navigation'
import type { AdminContext } from '@/core/auth/adminContext'

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: null,
    isOperator: false,
  }
}

describe('visibleNavItems', () => {
  it('shows every item to a super admin (* wildcard)', () => {
    const labels = visibleNavItems(ctx(['*'])).map((i) => i.label)
    expect(labels).toEqual(NAV_ITEMS.map((i) => i.label))
  })

  it('shows only the ungated item(s) with no permissions', () => {
    const labels = visibleNavItems(ctx([])).map((i) => i.label)
    expect(labels).toEqual(['Übersicht'])
  })

  it('shows only the ungated item(s) for a null context', () => {
    expect(visibleNavItems(null).map((i) => i.label)).toEqual(['Übersicht'])
  })

  it('shows exactly the items whose read gate the caller holds', () => {
    const labels = visibleNavItems(ctx(['user.read', 'job.read'])).map((i) => i.label)
    expect(labels).toEqual(['Übersicht', 'Benutzer', 'Jobs'])
  })

  it('every gated item points at an existing read permission convention', () => {
    // Guards against a typo in a permission key going unnoticed (all are `*.read`).
    for (const item of NAV_ITEMS) {
      if (item.permission) {
        expect(item.permission).toMatch(/^[a-z]+\.read$/)
      }
    }
  })
})
