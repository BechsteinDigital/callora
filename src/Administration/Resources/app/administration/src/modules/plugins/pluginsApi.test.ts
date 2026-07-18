import { describe, it, expect, vi } from 'vitest'
import { pluginsApi, isPluginActive, PluginState } from './pluginsApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

const okResult = { isSuccess: true, pluginId: 'acme', message: null, errorCode: null, warningMessage: null, warningCode: null }

describe('isPluginActive', () => {
  it('is true only for the Active state', () => {
    expect(isPluginActive(PluginState.Active)).toBe(true)
    expect(isPluginActive(PluginState.Installed)).toBe(false)
    expect(isPluginActive(PluginState.Inactive)).toBe(false)
  })
})

describe('pluginsApi', () => {
  it('lists installed plugins', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond([{ pluginId: 'acme', displayName: 'Acme', state: 1 }]))
    const list = await pluginsApi.list()
    expect(list[0].pluginId).toBe('acme')
  })

  it('lists from the reconciled /installed route', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([]))
    globalThis.fetch = fetchMock
    await pluginsApi.list()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/plugins/installed')
  })

  it('posts an activate to the plugin-scoped route', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(okResult))
    globalThis.fetch = fetchMock
    await pluginsApi.activate('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/plugins/acme/activate')
    expect(fetchMock.mock.calls[0][1].method).toBe('POST')
  })

  it('posts a deactivate to the plugin-scoped route', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(okResult))
    globalThis.fetch = fetchMock
    await pluginsApi.deactivate('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/plugins/acme/deactivate')
  })

  it('sends the local install payload', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(okResult))
    globalThis.fetch = fetchMock
    await pluginsApi.installLocal('acme', false)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/plugins/install/local')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ pluginId: 'acme', buildIfNeeded: false })
  })

  it('encodes the plugin id on uninstall', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(okResult))
    globalThis.fetch = fetchMock
    await pluginsApi.uninstall('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/plugins/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('surfaces the lifecycle failure message on a business failure (400)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(
      respond({ isSuccess: false, pluginId: 'acme', message: 'Abhängigkeit fehlt', errorCode: 'DEP_MISSING' }, 400),
    )
    await expect(pluginsApi.activate('acme')).rejects.toThrow('Abhängigkeit fehlt')
  })

  it('surfaces the lifecycle failure message on a forbidden result (403)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(
      respond({ isSuccess: false, pluginId: 'acme', message: 'Nicht erlaubt', errorCode: 'FORBIDDEN' }, 403),
    )
    await expect(pluginsApi.deactivate('acme')).rejects.toThrow('Nicht erlaubt')
  })

  it('returns the result including a warning on success', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(
      respond({ isSuccess: true, pluginId: 'acme', message: null, warningMessage: 'Vertrag veraltet' }),
    )
    const result = await pluginsApi.deactivate('acme')
    expect(result.warningMessage).toBe('Vertrag veraltet')
  })
})
