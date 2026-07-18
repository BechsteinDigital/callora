import { describe, it, expect, beforeEach } from 'vitest'
import { registerService, useService, resetServices } from './services'

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
})
