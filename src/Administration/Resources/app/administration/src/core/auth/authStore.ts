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

// Signing out ends the session LOCALLY, whatever the server answers. The call is the attempt
// to end it there too; failing at it is no reason to leave the operator signed in. Before this,
// a network error made `await` throw before `context` was cleared — the menu closed, no message
// appeared, the navigation to /login never ran, and the operator stayed in a session they had
// just ended (#291). A cookie the server could not revoke still expires; a UI that keeps looking
// signed in does not.
async function logout(): Promise<void> {
  try {
    await apiFetch('/api/auth/logout', { method: 'POST' })
  } catch {
    // Deliberately swallowed, and this is the one place where that is right: there is nothing
    // the operator could do with the error, and the action they asked for still happens.
  }

  context.value = null
}

function reset(): void {
  context.value = null
}

export function useAuthStore() {
  return { context, login, logout, loadContext, reset }
}
