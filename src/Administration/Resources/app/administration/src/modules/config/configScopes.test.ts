import { describe, expect, it } from 'vitest'
import { availableScopes, scopeOption } from './configScopes'
import { ConfigScope } from './systemConfigApi'
import type { AdminContext } from '@/core/auth/adminContext'

function ctx(isOperator: boolean): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions: ['*'],
    scope: null,
    workspaceKey: isOperator ? null : 'workspace-a',
    isOperator,
  }
}

describe('availableScopes', () => {
  it('offers all three levels to an operator', () => {
    expect(availableScopes(ctx(true)).map((s) => s.value)).toEqual([
      ConfigScope.Global,
      ConfigScope.Tenant,
      ConfigScope.Workspace,
    ])
  })

  it('offers only the workspace level to a workspace-bound admin', () => {
    // The server refuses global and tenant writes for them; offering the choice
    // would only produce a 403 after the fact.
    expect(availableScopes(ctx(false)).map((s) => s.value)).toEqual([ConfigScope.Workspace])
  })

  it('offers only the workspace level without a context', () => {
    expect(availableScopes(null).map((s) => s.value)).toEqual([ConfigScope.Workspace])
  })

  it('marks which scopes need a key', () => {
    const byValue = Object.fromEntries(availableScopes(ctx(true)).map((s) => [s.value, s.needsKey]))

    expect(byValue[ConfigScope.Global]).toBe(false)
    expect(byValue[ConfigScope.Tenant]).toBe(true)
    expect(byValue[ConfigScope.Workspace]).toBe(true)
  })

  it('explains every scope so the operator knows what they are changing', () => {
    for (const option of availableScopes(ctx(true))) {
      expect(option.description.length).toBeGreaterThan(0)
    }
  })
})

describe('scopeOption', () => {
  it('resolves a known scope', () => {
    expect(scopeOption(ConfigScope.Tenant)?.label).toBe('Mandant')
  })

  it('returns null for an unknown scope instead of guessing', () => {
    expect(scopeOption('surface')).toBeNull()
  })
})
