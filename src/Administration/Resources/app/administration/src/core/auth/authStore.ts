import { ref } from 'vue'
import { apiFetch } from '@/core/http'
import { parseAdminContext, type AdminContext } from '@/core/auth/adminContext'

const context = ref<AdminContext | null>(null)

async function loadContext(): Promise<boolean> {
  const res = await apiFetch('/api/admin/context')
  if (!res.ok) {
    context.value = null
    return false
  }
  context.value = parseAdminContext(await res.json())
  return true
}

async function login(loginName: string, password: string, workspaceKey: string | null): Promise<boolean> {
  const res = await apiFetch('/api/auth/login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ login: loginName, password, workspaceKey }),
  })
  if (!res.ok) {
    return false
  }
  return loadContext()
}

async function logout(): Promise<void> {
  await apiFetch('/api/auth/logout', { method: 'POST' })
  context.value = null
}

function reset(): void {
  context.value = null
}

export function useAuthStore() {
  return { context, login, logout, loadContext, reset }
}
