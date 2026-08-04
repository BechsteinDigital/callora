import { describe, expect, it } from 'vitest'
import { isNavItemActive } from './navActive'

describe('isNavItemActive', () => {
  it('highlights a section on its own route', () => {
    expect(isNavItemActive('/users', '/users')).toBe(true)
  })

  it('keeps a section highlighted inside its detail routes', () => {
    expect(isNavItemActive('/users', '/users/u-1')).toBe(true)
    expect(isNavItemActive('/users', '/users/new')).toBe(true)
  })

  it('does not highlight a section for a merely similar path', () => {
    expect(isNavItemActive('/user', '/users')).toBe(false)
    expect(isNavItemActive('/workspaces', '/workspace-settings')).toBe(false)
  })

  it('highlights the dashboard only on the root path', () => {
    expect(isNavItemActive('/', '/')).toBe(true)
    expect(isNavItemActive('/', '/users')).toBe(false)
    expect(isNavItemActive('/', '/plugins/x')).toBe(false)
  })

  it('treats a trailing slash as the same page', () => {
    expect(isNavItemActive('/users', '/users/')).toBe(true)
    expect(isNavItemActive('/users/', '/users')).toBe(true)
  })

  it('highlights a plugin entry inside its extension host route', () => {
    expect(isNavItemActive('/extensions/communication', '/extensions/communication')).toBe(true)
    expect(isNavItemActive('/extensions/communication', '/extensions/videoconference')).toBe(false)
  })
})
