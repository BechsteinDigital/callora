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
      defaultSurfaceBaseUrl: null,
      publicHost: null,
    })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      displayName: 'Acme',
      workspaceType: 'standard',
      isActive: true,
      defaultSurfaceBaseUrl: null,
      publicHost: null,
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
      workspacesApi.upsert('acme', { displayName: 'A', workspaceType: 't', isActive: true, defaultSurfaceBaseUrl: 'bad', publicHost: null }),
    ).rejects.toThrow('Workspace public URL is invalid.')
  })

  it('returns the first member page with a limit', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(
      respond({ items: [{ userId: 'alice', role: 'admin' }], total: 1, nextCursor: null }),
    )
    globalThis.fetch = fetchMock
    const page = await workspacesApi.listMembers('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/members?limit=50')
    expect(page.items).toEqual([{ userId: 'alice', role: 'admin' }])
    expect(page.nextCursor).toBeNull()
  })

  it('passes the cursor for the next member page', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ items: [], total: 60, nextCursor: null }))
    globalThis.fetch = fetchMock
    await workspacesApi.listMembers('acme', 'cur123')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/members?limit=50&cursor=cur123')
  })

  it('upserts a member via the member route with the role body', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ userId: 'a b', role: 'admin' }))
    globalThis.fetch = fetchMock
    await workspacesApi.upsertMember('acme', 'a b', 'admin')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/members/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ role: 'admin' })
  })

  it('removes a member via DELETE with an encoded user id', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await workspacesApi.removeMember('acme', 'a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/members/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('lists surfaces from the workspace sub-resource', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([{ surfaceKey: 'default', displayName: 'Default' }]))
    globalThis.fetch = fetchMock
    const list = await workspacesApi.listSurfaces('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/surfaces')
    expect(list[0].surfaceKey).toBe('default')
  })

  it('upserts a surface via PUT with the full field set and an encoded key', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ surfaceKey: 'portal' }))
    globalThis.fetch = fetchMock
    await workspacesApi.upsertSurface('acme', 'a b', {
      displayName: 'Portal',
      surfaceType: 'spa',
      publicBaseUrl: null,
      publicHost: 'portal.example.de',
      publicPathPrefix: '/',
      accessMode: 'Authenticated',
      routing: 'Tree',
      locale: 'de',
      templatePluginId: null,
      templateVersion: null,
      themePluginId: 'customer.theme',
      themeVersion: '1.0.0',
      isActive: true,
      parentSurfaceKey: 'portal',
      position: 3,
      requiredClaims: 'partner',
    })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/surfaces/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    expect(body.accessMode).toBe('Authenticated')
    expect(body.themePluginId).toBe('customer.theme') // carried theme survives the round-trip
    // Der Baum reist mit: Ohne diese beiden Felder legte die Verwaltung nur Wurzeln an.
    expect(body.parentSurfaceKey).toBe('portal')
    expect(body.position).toBe(3)
  })

  it('removes a surface via DELETE with an encoded key', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await workspacesApi.removeSurface('acme', 'a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/acme/surfaces/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('lists plugin assignments from the workspace sub-resource', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(
      respond([{ pluginId: 'videoconference', isAssigned: false }]),
    )
    globalThis.fetch = fetchMock

    const list = await workspacesApi.listPlugins('a b')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workspaces/a%20b/plugins')
    expect(list[0].pluginId).toBe('videoconference')
  })

  it('assigns a plugin via PUT and sends the desired assignment state', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(
      respond({ pluginId: 'video conference', isAssigned: true }),
    )
    globalThis.fetch = fetchMock

    await workspacesApi.setPluginAssignment('a b', 'video conference', true)

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/workspaces/a%20b/plugins/video%20conference',
    )
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      isAssigned: true,
    })
  })
})
