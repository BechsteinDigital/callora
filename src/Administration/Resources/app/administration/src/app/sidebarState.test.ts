import { beforeEach, describe, expect, it, vi } from 'vitest'
import { initSidebar, resetSidebar, useSidebar } from './sidebarState'

describe('sidebar state', () => {
  beforeEach(() => {
    localStorage.clear()
    resetSidebar()
  })

  it('starts expanded on a fresh install', () => {
    initSidebar()

    expect(useSidebar().collapsed.value).toBe(false)
  })

  it('remembers a collapsed sidebar across sessions', () => {
    const { toggleCollapsed } = useSidebar()

    toggleCollapsed()
    resetSidebar()
    initSidebar()

    expect(useSidebar().collapsed.value).toBe(true)
  })

  it('remembers expanding it again', () => {
    const { toggleCollapsed } = useSidebar()
    toggleCollapsed()
    toggleCollapsed()

    resetSidebar()
    initSidebar()

    expect(useSidebar().collapsed.value).toBe(false)
  })

  it('never restores the mobile drawer as open — it is a momentary state', () => {
    const { openMobile } = useSidebar()
    openMobile()

    resetSidebar()
    initSidebar()

    expect(useSidebar().mobileOpen.value).toBe(false)
  })

  it('opens and closes the mobile drawer', () => {
    const { openMobile, closeMobile, mobileOpen } = useSidebar()

    openMobile()
    expect(mobileOpen.value).toBe(true)

    closeMobile()
    expect(mobileOpen.value).toBe(false)
  })

  it('shares one state across every consumer', () => {
    const first = useSidebar()
    const second = useSidebar()

    first.toggleCollapsed()

    expect(second.collapsed.value).toBe(true)
  })

  it('still toggles when storage is unavailable', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage disabled')
    })
    const { toggleCollapsed, collapsed } = useSidebar()

    expect(() => toggleCollapsed()).not.toThrow()
    expect(collapsed.value).toBe(true)

    setItem.mockRestore()
  })

  it('falls back to expanded when reading storage throws', () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage disabled')
    })

    initSidebar()

    expect(useSidebar().collapsed.value).toBe(false)
    getItem.mockRestore()
  })
})
