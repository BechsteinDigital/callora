import { describe, it, expect, vi } from 'vitest'
import { themesApi } from './themesApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('themesApi', () => {
  it('lists active workspace theme definitions', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([{ templateKey: 't', pluginId: 'p', version: '1.0.0' }]))
    globalThis.fetch = fetchMock
    const defs = await themesApi.listDefinitions()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/themes/definitions?surface=workspace&active=true')
    expect(defs[0].pluginId).toBe('p')
  })

  it('returns the assignment for a workspace', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ workspaceKey: 'acme', themePluginId: 'p', themeVersion: '1.0.0' }))
    globalThis.fetch = fetchMock
    const assignment = await themesApi.getAssignment('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/themes/workspaces/a%20b')
    expect(assignment?.themePluginId).toBe('p')
  })

  it('returns null when no theme is assigned (404)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond(null, 404))
    expect(await themesApi.getAssignment('acme')).toBeNull()
  })

  it('assigns a theme via PUT with plugin id and version', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ workspaceKey: 'acme', themePluginId: 'p' }))
    globalThis.fetch = fetchMock
    await themesApi.assign('acme', 'customer.theme', '2.0.0')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/themes/workspaces/acme')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      themePluginId: 'customer.theme',
      themeVersion: '2.0.0',
      assignedBy: null,
    })
  })

  it('clears the assignment via DELETE', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await themesApi.clearAssignment('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/themes/workspaces/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('surfaces the RFC 9457 problem detail on a rejected assign', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'No active workspace theme definitions.' }, 400))
    await expect(themesApi.assign('acme', 'x', '1')).rejects.toThrow('No active workspace theme definitions.')
  })

  it('gets the theme settings for a workspace', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ workspaceKey: 'acme', hasAssignedTheme: true, fields: [], valuesByKey: {} }))
    globalThis.fetch = fetchMock
    const settings = await themesApi.getSettings('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/themes/workspaces/a%20b/settings')
    expect(settings.hasAssignedTheme).toBe(true)
  })

  it('saves the theme settings via PUT wrapping valuesByKey', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ workspaceKey: 'acme', hasAssignedTheme: true, fields: [], valuesByKey: {} }))
    globalThis.fetch = fetchMock
    await themesApi.saveSettings('acme', { primaryColor: '#fff', maxItems: 5 })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/themes/workspaces/acme/settings')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      valuesByKey: { primaryColor: '#fff', maxItems: 5 },
    })
  })
})
