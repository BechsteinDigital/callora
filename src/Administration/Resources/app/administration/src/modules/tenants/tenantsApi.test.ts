import { describe, it, expect, vi } from 'vitest'
import { tenantsApi } from './tenantsApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('tenantsApi', () => {
  it('lists tenants', async () => {
    globalThis.fetch = vi
      .fn()
      .mockResolvedValueOnce(respond([{ tenantKey: 'acme', displayName: 'Acme', isActive: true }]))
    const tenants = await tenantsApi.list()
    expect(tenants[0].tenantKey).toBe('acme')
  })

  it('posts key and display name on create', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ tenantKey: 'acme', displayName: 'Acme' }))
    globalThis.fetch = fetchMock
    await tenantsApi.create('acme', 'Acme')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/tenants')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({ tenantKey: 'acme', displayName: 'Acme' })
  })

  it('encodes the key in the activate route', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null))
    globalThis.fetch = fetchMock
    await tenantsApi.activate('a/b')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/tenants/a%2Fb/activate')
    expect(init.method).toBe('POST')
  })

  it('suspends via the suspend route', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null))
    globalThis.fetch = fetchMock
    await tenantsApi.suspend('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/tenants/acme/suspend')
    expect(fetchMock.mock.calls[0][1].method).toBe('POST')
  })

  it('deletes via DELETE on the key route', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null))
    globalThis.fetch = fetchMock
    await tenantsApi.remove('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/tenants/acme')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('throws the problem detail on a rejected create', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'Tenant already exists.' }, 409))
    await expect(tenantsApi.create('acme', 'Acme')).rejects.toThrow('Tenant already exists.')
  })
})
