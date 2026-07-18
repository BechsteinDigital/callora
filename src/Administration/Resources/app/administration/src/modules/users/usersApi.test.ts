import { describe, it, expect, vi } from 'vitest'
import { usersApi } from './usersApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('usersApi', () => {
  it('lists users', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond([{ externalId: 'admin', hasPassword: true }]))
    const users = await usersApi.list()
    expect(users[0].externalId).toBe('admin')
  })

  it('sends the create payload to /api/users', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ externalId: 'op' }, 201))
    globalThis.fetch = fetchMock
    await usersApi.create({ externalId: 'op', email: 'op@x.io', displayName: 'Op', password: 'secret' })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/users')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({
      externalId: 'op',
      email: 'op@x.io',
      displayName: 'Op',
      password: 'secret',
    })
  })

  it('keeps a null password on update so it is not overwritten', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ externalId: 'op' }))
    globalThis.fetch = fetchMock
    await usersApi.update('op', { email: 'new@x.io', displayName: 'Op', password: null })
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string).password).toBeNull()
  })

  it('encodes the user id in the path', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await usersApi.remove('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/users/a%20b')
  })

  it('assigns a role through the rbac endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null))
    globalThis.fetch = fetchMock
    await usersApi.assignRole('op', 'superadmin')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/security/rbac/users/op')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ role: 'superadmin' })
  })

  it('flattens role assignments into a userId → role map', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(
      respond([
        { userId: 'op', role: 'superadmin' },
        { userId: 'alice', role: 'admin' },
      ]),
    )
    const map = await usersApi.listRoleAssignments()
    expect(map).toEqual({ op: 'superadmin', alice: 'admin' })
  })

  it('throws the problem detail on a failed request', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'External id already exists.' }, 400))
    await expect(
      usersApi.create({ externalId: 'dup', email: null, displayName: null, password: 'x' }),
    ).rejects.toThrow('External id already exists.')
  })
})
