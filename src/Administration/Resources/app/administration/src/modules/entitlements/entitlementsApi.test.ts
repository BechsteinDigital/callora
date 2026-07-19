import { describe, it, expect, vi } from 'vitest'
import { entitlementsApi } from './entitlementsApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('entitlementsApi', () => {
  it('lists entitlements from /api/entitlements', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([{ pluginId: 'acme.plugin', isEntitled: true }]))
    globalThis.fetch = fetchMock
    const list = await entitlementsApi.list()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/entitlements')
    expect(list[0].pluginId).toBe('acme.plugin')
  })

  it('sets an entitlement via PUT with the full input', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await entitlementsApi.set({
      pluginId: 'acme.plugin',
      tenantKey: 'tenant-a',
      workspaceKey: null,
      isEntitled: true,
    })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/entitlements')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      pluginId: 'acme.plugin',
      tenantKey: 'tenant-a',
      workspaceKey: null,
      isEntitled: true,
    })
  })

  it('surfaces the RFC 9457 problem detail on error', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'pluginId is required.' }, 400))
    await expect(
      entitlementsApi.set({ pluginId: '', tenantKey: null, workspaceKey: null, isEntitled: true }),
    ).rejects.toThrow('pluginId is required.')
  })
})
