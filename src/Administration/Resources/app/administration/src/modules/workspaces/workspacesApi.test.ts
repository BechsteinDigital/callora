import { describe, it, expect, vi } from 'vitest'
import { workspacesApi } from './workspacesApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('workspacesApi', () => {
  it('lists workspaces from /api/workspaces', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([{ workspaceKey: 'acme', displayName: 'Acme' }]))
    globalThis.fetch = fetchMock
    const list = await workspacesApi.list()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces')
    expect(list[0].workspaceKey).toBe('acme')
  })

  it('gets a single workspace with an encoded key', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ workspaceKey: 'a b' }))
    globalThis.fetch = fetchMock
    await workspacesApi.get('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/a%20b')
  })

  it('upserts via PUT with the mutable slice', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ workspaceKey: 'acme' }))
    globalThis.fetch = fetchMock
    await workspacesApi.upsert('acme', {
      displayName: 'Acme',
      workspaceType: 'standard',
      isActive: true,
      publicBaseUrl: null,
    })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      displayName: 'Acme',
      workspaceType: 'standard',
      isActive: true,
      publicBaseUrl: null,
    })
  })

  it('deletes via DELETE with an encoded key', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await workspacesApi.remove('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('surfaces the RFC 9457 problem detail on error', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'Workspace public URL is invalid.' }, 400))
    await expect(
      workspacesApi.upsert('acme', { displayName: 'A', workspaceType: 't', isActive: true, publicBaseUrl: 'bad' }),
    ).rejects.toThrow('Workspace public URL is invalid.')
  })
})
