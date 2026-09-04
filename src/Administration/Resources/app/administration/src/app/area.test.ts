import { describe, expect, it } from 'vitest'
import { currentArea, currentAreaSubject } from './area'
import { availableAreas, visibleNavItems } from './navigation'
import type { AdminContext } from '@/core/auth/adminContext'

function ctx(partial: Partial<AdminContext>): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions: ['*'],
    scope: null,
    workspaceKey: null,
    tenantKey: null,
    isOperator: false,
    ...partial,
  }
}

describe('currentArea', () => {
  it('reads the area off the scope, not off the permissions', () => {
    // Ein Rechtesatz kann versehentlich mandantenhaft aussehen, ein Scope nicht.
    expect(currentArea(ctx({ scope: 'tenant', tenantKey: 't' }))).toBe('tenant')
    expect(currentArea(ctx({ scope: 'workspace', workspaceKey: 'w' }))).toBe('workspace')
    expect(currentArea(ctx({ scope: 'platform' }))).toBe('platform')
  })

  it('puts an operator on the platform even without a scope claim', () => {
    // Dieselbe Regel wie im AdminLoginResolver: Ein Betreiber wird nie herabgestuft.
    expect(currentArea(ctx({ isOperator: true }))).toBe('platform')
  })

  it('has no area before sign-in', () => {
    expect(currentArea(null)).toBeNull()
  })

  it('names the tenant or workspace one is in', () => {
    // „Mandant" ohne den Namen des Mandanten ist eine Überschrift, die niemandem sagt, wo er ist.
    expect(currentAreaSubject(ctx({ scope: 'tenant', tenantKey: 'acme' }))).toBe('acme')
    expect(currentAreaSubject(ctx({ scope: 'workspace', workspaceKey: 'vertrieb' }))).toBe('vertrieb')
    expect(currentAreaSubject(ctx({ scope: 'platform' }))).toBeNull()
  })
})

describe('availableAreas', () => {
  it('gives an operator all three', () => {
    expect(availableAreas(ctx({ isOperator: true }))).toEqual(['platform', 'tenant', 'workspace'])
  })

  it('gives everyone else the one their session is', () => {
    // Woanders hinzukommen heißt eine neue Sitzung (POST /api/auth/scope), keinen Klick.
    expect(availableAreas(ctx({ scope: 'tenant', tenantKey: 't' }))).toEqual(['tenant'])
    expect(availableAreas(ctx({ scope: 'workspace', workspaceKey: 'w' }))).toEqual(['workspace'])
  })

  it('gives nobody an area before sign-in', () => {
    expect(availableAreas(null)).toEqual([])
  })
})

describe('visibleNavItems with areas', () => {
  it('keeps workspace work out of the tenant area', () => {
    // Ein Mandanten-Administrator trägt workspace.read — ohne Bereiche stünden die Flächen
    // in seiner Navigation, obwohl sie zur Arbeit IM Workspace gehören.
    const labels = visibleNavItems(ctx({ scope: 'tenant', tenantKey: 't' })).map((i) => i.label)

    expect(labels).toContain('Workspaces')
    expect(labels).not.toContain('Flächen')
    expect(labels).not.toContain('Mandanten')
  })

  it('falls back to the permission filter alone when there is no area', () => {
    // Vor der Anmeldung und für eine Sitzung ohne Scope: ausblenden hieße raten.
    const labels = visibleNavItems(ctx({ permissions: ['media.read'] })).map((i) => i.label)

    expect(labels).toContain('Medien')
  })

  it('every item belongs to at least one area', () => {
    // Ein Punkt ohne Bereich wäre unerreichbar, ohne dass irgendwo etwas fehlschlägt.
    for (const item of visibleNavItems(ctx({ isOperator: true }), null)) {
      expect(item.areas.length).toBeGreaterThan(0)
    }
  })
})
