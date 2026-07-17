import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useAuthStore } from './authStore'

beforeEach(() => useAuthStore().reset())

describe('authStore', () => {
  it('loads context after a successful login', async () => {
    const store = useAuthStore()
    globalThis.fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ userId: 'u1', isOperator: true }), { status: 200 }))

    const ok = await store.login('root', 'pass', null)

    expect(ok).toBe(true)
    expect(store.context.value?.userId).toBe('u1')
  })

  it('returns false and keeps no context on a rejected login', async () => {
    const store = useAuthStore()
    globalThis.fetch = vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 }))

    const ok = await store.login('x', 'y', null)

    expect(ok).toBe(false)
    expect(store.context.value).toBeNull()
  })

  it('sends the workspace key in the login body', async () => {
    const store = useAuthStore()
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ userId: 'alice', isOperator: false }), { status: 200 }))
    globalThis.fetch = fetchMock

    await store.login('alice', 'pass-1', 'workspace-a')

    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    expect(body).toEqual({ login: 'alice', password: 'pass-1', workspaceKey: 'workspace-a' })
  })
})
