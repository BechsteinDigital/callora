import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'
import { authGuard } from './routeGuard'
import { useAuthStore } from '@/core/auth/authStore'

beforeEach(() => useAuthStore().reset())

function route(path: string, meta: Record<string, unknown> = {}): RouteLocationNormalized {
  return { path, meta } as unknown as RouteLocationNormalized
}

describe('authGuard', () => {
  it('allows the public login route through without loading context', async () => {
    expect(await authGuard(route('/login', { public: true }))).toBe(true)
  })

  it('allows protected routes when a context is already present (no reload)', async () => {
    useAuthStore().context.value = { userId: 'u1' } as never
    expect(await authGuard(route('/'))).toBe(true)
  })

  it('rehydrates from the cookie session on hard reload and allows the route', async () => {
    globalThis.fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ userId: 'u1', isOperator: true }), { status: 200 }))

    const result = await authGuard(route('/'))

    expect(result).toBe(true)
    expect(useAuthStore().context.value?.userId).toBe('u1')
  })

  it('redirects to /login when no context and rehydration fails (no/expired session)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(new Response(null, { status: 401 }))

    expect(await authGuard(route('/'))).toBe('/login')
    expect(useAuthStore().context.value).toBeNull()
  })
})
