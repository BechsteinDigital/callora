import { describe, it, expect, beforeEach } from 'vitest'
import { registerService, useService, resetServices, getServiceConflicts } from './services'

beforeEach(() => resetServices())

describe('service overrides', () => {
  it('returns the fallback when no override is registered', () => {
    const fallback = { name: 'core' }
    expect(useService('usersApi', fallback)).toBe(fallback)
  })

  it('returns the registered override instead of the fallback', () => {
    const fallback = { name: 'core' }
    const override = { name: 'plugin' }
    registerService('usersApi', override)
    expect(useService('usersApi', fallback)).toBe(override)
  })

  it('isolates keys from one another', () => {
    registerService('rolesApi', { x: 1 })
    const fallback = { x: 2 }
    expect(useService('usersApi', fallback)).toBe(fallback)
  })

  it('lets the highest-priority registration win regardless of order', () => {
    const low = { name: 'low' }
    const high = { name: 'high' }
    registerService('usersApi', high, { pluginId: 'a', priority: 100 })
    registerService('usersApi', low, { pluginId: 'b', priority: 10 })
    expect(useService('usersApi', { name: 'core' })).toBe(high)
  })

  it('breaks a priority tie in favour of the last registration', () => {
    const first = { name: 'first' }
    const second = { name: 'second' }
    registerService('usersApi', first, { pluginId: 'a' })
    registerService('usersApi', second, { pluginId: 'b' })
    expect(useService('usersApi', { name: 'core' })).toBe(second)
  })

  it('reports a conflict with the active owner and the shadowed owners', () => {
    registerService('usersApi', { name: 'a' }, { pluginId: 'a', priority: 1 })
    registerService('usersApi', { name: 'b' }, { pluginId: 'b', priority: 5 })
    expect(getServiceConflicts()).toEqual([
      { key: 'usersApi', activePluginId: 'b', shadowedPluginIds: ['a'] },
    ])
  })

  it('reports no conflict for a single registration', () => {
    registerService('usersApi', { name: 'a' }, { pluginId: 'a' })
    expect(getServiceConflicts()).toEqual([])
  })
})
