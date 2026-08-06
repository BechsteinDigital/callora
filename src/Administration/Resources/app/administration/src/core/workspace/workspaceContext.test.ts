import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useWorkspaceContext, resetWorkspaceContext, readStoredWorkspace } from './workspaceContext'
import type { AdminContext } from '@/core/auth/adminContext'

const { listMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: listMock } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

function ctx(workspaceKey: string | null): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions: ['*'],
    scope: null,
    workspaceKey,
    isOperator: workspaceKey === null,
  }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([
    { workspaceKey: 'wsA', displayName: 'A' },
    { workspaceKey: 'wsB', displayName: 'B' },
  ])
  resetWorkspaceContext()
})

describe('workspaceContext', () => {
  it('uses the token workspace for a bound admin and never loads the list', async () => {
    contextRef.value = ctx('ws1')
    const { activeWorkspace, fixedWorkspace, canSwitch, ensure } = useWorkspaceContext()
    await ensure()

    expect(activeWorkspace.value).toBe('ws1')
    expect(fixedWorkspace.value).toBe('ws1')
    expect(canSwitch.value).toBe(false)
    expect(listMock).not.toHaveBeenCalled()
  })

  it('loads the operator list and defaults to the first workspace', async () => {
    contextRef.value = ctx(null)
    const { activeWorkspace, workspaces, canSwitch, ensure } = useWorkspaceContext()
    await ensure()

    expect(listMock).toHaveBeenCalledTimes(1)
    expect(workspaces.value).toHaveLength(2)
    expect(activeWorkspace.value).toBe('wsA')
    expect(canSwitch.value).toBe(true)
  })

  it('setActive switches the active workspace and persists it', async () => {
    contextRef.value = ctx(null)
    const { activeWorkspace, ensure, setActive } = useWorkspaceContext()
    await ensure()

    setActive('wsB')

    expect(activeWorkspace.value).toBe('wsB')
    expect(localStorage.getItem('callora.activeWorkspace')).toBe('wsB')
  })

  it('setActive reloads the shell, because plugin bundles are chained per workspace', async () => {
    contextRef.value = ctx(null)
    const reload = vi.fn()
    const { ensure, setActive } = useWorkspaceContext()
    await ensure()

    setActive('wsB', reload)

    // The loaded bundles belong to the previous workspace and a script cannot be unloaded.
    // Reloading is the only state that is not half-right.
    expect(reload).toHaveBeenCalledOnce()
  })

  it('setActive does not reload when the workspace did not actually change', async () => {
    contextRef.value = ctx(null)
    const reload = vi.fn()
    const { ensure, setActive } = useWorkspaceContext()
    await ensure()

    setActive('wsA', reload)

    expect(reload).not.toHaveBeenCalled()
  })

  it('readStoredWorkspace exposes the persisted selection for the bootstrap chain request', () => {
    localStorage.setItem('callora.activeWorkspace', 'wsB')

    expect(readStoredWorkspace()).toBe('wsB')
  })

  it('readStoredWorkspace returns null when nothing is persisted', () => {
    expect(readStoredWorkspace()).toBeNull()
  })

  it('restores a persisted selection when it still exists', async () => {
    localStorage.setItem('callora.activeWorkspace', 'wsB')
    contextRef.value = ctx(null)
    const { activeWorkspace, ensure } = useWorkspaceContext()
    await ensure()

    expect(activeWorkspace.value).toBe('wsB')
  })

  it('loads the list only once across repeated ensure calls', async () => {
    contextRef.value = ctx(null)
    const { ensure } = useWorkspaceContext()
    await ensure()
    await ensure()

    expect(listMock).toHaveBeenCalledTimes(1)
  })

  it('does not cache a failed load, allowing a later retry', async () => {
    contextRef.value = ctx(null)
    listMock.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce([{ workspaceKey: 'wsA', displayName: 'A' }])
    const { activeWorkspace, ensure } = useWorkspaceContext()

    await expect(ensure()).rejects.toThrow('offline')
    await ensure() // retries because the rejected promise was not cached

    expect(listMock).toHaveBeenCalledTimes(2)
    expect(activeWorkspace.value).toBe('wsA')
  })
})
