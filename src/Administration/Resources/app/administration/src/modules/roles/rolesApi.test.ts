import { describe, it, expect, vi } from 'vitest'
import { rolesApi } from './rolesApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('rolesApi', () => {
  it('lists roles', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond([{ role: 'superadmin', permissions: ['*'] }]))
    const roles = await rolesApi.list()
    expect(roles[0].role).toBe('superadmin')
  })

  it('lists permissions', async () => {
    globalThis.fetch = vi
      .fn()
      .mockResolvedValueOnce(respond([{ permissionKey: 'user.read', function: 'user', action: 'read' }]))
    const perms = await rolesApi.listPermissions()
    expect(perms[0].function).toBe('user')
  })

  it('groups flat permission keys into { function, actions } on upsert', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null))
    globalThis.fetch = fetchMock
    await rolesApi.upsert('support', ['user.read', 'user.update', 'role.read'])
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/security/rbac/roles/support')
    expect(init.method).toBe('PUT')
    expect(JSON.parse(init.body as string).functions).toEqual([
      { function: 'user', actions: ['read', 'update'] },
      { function: 'role', actions: ['read'] },
    ])
  })

  it('sends an empty functions list when no permissions are selected', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null))
    globalThis.fetch = fetchMock
    await rolesApi.upsert('empty', [])
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string).functions).toEqual([])
  })

  it('throws the problem detail on a rejected delete', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'Role is fixed.' }, 400))
    await expect(rolesApi.remove('superadmin')).rejects.toThrow('Role is fixed.')
  })
})
