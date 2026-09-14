import { describe, it, expect } from 'vitest'
import { NAV_ITEMS, visibleNavGroups, visibleNavItems } from './navigation'
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
    tenantKey: null,
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

describe('visibleNavGroups', () => {
  it('arranges the items into the sidebar sections, dashboard first and untitled', () => {
    const groups = visibleNavGroups(ctx(['*']))

    expect(groups[0]).toMatchObject({ id: 'overview', label: null })
    expect(groups[0].items.map((i) => i.label)).toEqual(['Übersicht'])
    expect(groups.map((g) => g.id)).toEqual(['overview', 'management', 'content', 'system'])
  })

  it('drops a group whose items the caller may not see', () => {
    const groups = visibleNavGroups(ctx(['user.read']))

    expect(groups.map((g) => g.id)).toEqual(['overview', 'management'])
    expect(groups[1].items.map((i) => i.label)).toEqual(['Benutzer'])
  })

  it('leaves only the dashboard for a context without permissions', () => {
    const groups = visibleNavGroups(ctx([]))

    expect(groups).toHaveLength(1)
    expect(groups[0].items.map((i) => i.label)).toEqual(['Übersicht'])
  })

  it('never emits an empty group', () => {
    for (const group of visibleNavGroups(ctx(['*']))) {
      expect(group.items.length).toBeGreaterThan(0)
    }
  })

  it('covers every nav item across the groups, without duplicates', () => {
    const grouped = visibleNavGroups(ctx(['*'])).flatMap((g) => g.items.map((i) => i.to))

    expect(new Set(grouped).size).toBe(grouped.length)
    expect(grouped.sort()).toEqual(NAV_ITEMS.map((i) => i.to).sort())
  })

  it('gives every item an icon so the collapsed rail stays usable', () => {
    for (const item of NAV_ITEMS) {
      expect(item.icon).toBeTruthy()
    }
  })
})
