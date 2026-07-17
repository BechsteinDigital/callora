import type { RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
  }
}

/**
 * Client-side navigation guard: public routes always pass; every other route
 * requires a loaded admin context, otherwise it redirects to /login. This is a
 * UX gate only — server-side authorization stays authoritative.
 */
export function authGuard(to: RouteLocationNormalized): true | string {
  if (to.meta.public) {
    return true
  }
  return useAuthStore().context.value ? true : '/login'
}
