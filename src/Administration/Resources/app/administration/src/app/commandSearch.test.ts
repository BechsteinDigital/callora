import { describe, expect, it } from 'vitest'
import { searchCommands } from './commandSearch'
import type { CommandItem } from './commandItem'

const commands: readonly CommandItem[] = [
  { id: 'users', label: 'Benutzer', section: 'Verwaltung', to: '/users', keywords: ['user', 'accounts'] },
  { id: 'user-new', label: 'Benutzer anlegen', section: 'Aktionen', to: '/users/new' },
  { id: 'roles', label: 'Rollen', section: 'Verwaltung', to: '/roles', keywords: ['rbac', 'permissions'] },
  { id: 'plugins', label: 'Plugins', section: 'System', to: '/plugins' },
  { id: 'entitlements', label: 'Berechtigungen', section: 'System', to: '/entitlements', keywords: ['plugins'] },
  { id: 'logout', label: 'Abmelden', section: 'Aktionen', keywords: ['logout', 'sign out'] },
]

function labels(query: string): string[] {
  return searchCommands(commands, query).map((c) => c.label)
}

describe('searchCommands', () => {
  it('returns everything for an empty query', () => {
    expect(searchCommands(commands, '')).toHaveLength(commands.length)
  })

  it('ignores surrounding whitespace', () => {
    expect(labels('  rollen  ')).toEqual(['Rollen'])
  })

  it('matches case-insensitively', () => {
    expect(labels('BENUTZER')).toContain('Benutzer')
  })

  it('ranks an exact label above a longer label that merely starts with it', () => {
    expect(labels('benutzer')).toEqual(['Benutzer', 'Benutzer anlegen'])
  })

  it('finds an entry through its keywords', () => {
    expect(labels('rbac')).toEqual(['Rollen'])
    expect(labels('logout')).toEqual(['Abmelden'])
  })

  it('ranks a label hit above a keyword hit for the same term', () => {
    const results = labels('plugins')

    expect(results).toEqual(['Plugins', 'Berechtigungen'])
  })

  it('finds an entry whose German label shares no letters with the typed English term', () => {
    expect(labels('accounts')).toEqual(['Benutzer'])
  })

  it('finds entries by their section', () => {
    expect(labels('aktionen')).toEqual(['Abmelden', 'Benutzer anlegen'])
  })

  it('returns nothing when nothing matches', () => {
    expect(labels('zzz')).toEqual([])
  })

  it('does not mutate the input list', () => {
    const before = [...commands]

    searchCommands(commands, 'benutzer')

    expect(commands).toEqual(before)
  })
})
