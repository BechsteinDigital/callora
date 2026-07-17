import type { RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
  }
}

/**
 * Client-side navigation guard. Public routes always pass. For protected routes
 * the admin context must be present; on a hard reload it is empty, so the guard
 * rehydrates it once from the cookie session via /api/admin/context. If that
 * fails (no/expired session) it redirects to /login. This is a UX gate only —
 * server-side authorization stays authoritative.
 */
export async function authGuard(to: RouteLocationNormalized): Promise<true | string> {
  if (to.meta.public) {
    return true
  }

  const store = useAuthStore()
  if (store.context.value) {
    return true
  }

  return (await store.loadContext()) ? true : '/login'
}
