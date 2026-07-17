import { describe, it, expect, beforeEach } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'
import { authGuard } from './routeGuard'
import { useAuthStore } from '@/core/auth/authStore'

beforeEach(() => useAuthStore().reset())

function route(path: string, meta: Record<string, unknown> = {}): RouteLocationNormalized {
  return { path, meta } as unknown as RouteLocationNormalized
}

describe('authGuard', () => {
  it('redirects to /login when no context and route is protected', () => {
    expect(authGuard(route('/'))).toBe('/login')
  })

  it('allows the public login route through without context', () => {
    expect(authGuard(route('/login', { public: true }))).toBe(true)
  })

  it('allows protected routes when a context is present', () => {
    useAuthStore().context.value = { userId: 'u1' } as never
    expect(authGuard(route('/'))).toBe(true)
  })
})
