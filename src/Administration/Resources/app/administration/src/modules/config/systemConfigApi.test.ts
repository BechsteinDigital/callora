import { describe, it, expect, vi } from 'vitest'
import { systemConfigApi, isSecretField } from './systemConfigApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('isSecretField', () => {
  it('detects the secret field type case-insensitively', () => {
    expect(isSecretField('secret')).toBe(true)
    expect(isSecretField('Secret')).toBe(true)
    expect(isSecretField('text')).toBe(false)
  })
})

describe('systemConfigApi', () => {
  it('lists all definitions without a plugin filter', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([{ pluginId: 'acme', configKey: 'k' }]))
    globalThis.fetch = fetchMock
    await systemConfigApi.listDefinitions()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/config/definitions')
  })

  it('filters definitions by plugin', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([]))
    globalThis.fetch = fetchMock
    await systemConfigApi.listDefinitions('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/config/definitions?pluginId=a%20b')
  })

  it('reads effective values for a plugin (global)', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ pluginId: 'acme', workspaceKey: null, valuesByKey: {} }))
    globalThis.fetch = fetchMock
    await systemConfigApi.effective('acme')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/config/effective?pluginId=acme')
  })

  it('includes the workspace key when given', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ pluginId: 'acme', workspaceKey: 'ws1', valuesByKey: {} }))
    globalThis.fetch = fetchMock
    await systemConfigApi.effective('acme', { workspaceKey: 'ws1' })
    expect(fetchMock.mock.calls[0][0]).toContain('workspaceKey=ws1')
  })

  it('includes the tenant key when asking for the tenant view', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ pluginId: 'acme', tenantKey: 't1', valuesByKey: {} }))
    globalThis.fetch = fetchMock
    await systemConfigApi.effective('acme', { tenantKey: 't1' })
    expect(fetchMock.mock.calls[0][0]).toContain('tenantKey=t1')
  })

  it('puts values with the scope envelope', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await systemConfigApi.saveValues('acme', 'global', null, { greeting: 'hi', retries: 3 })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/config/values')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      pluginId: 'acme',
      scope: 'global',
      scopeKey: null,
      valuesByKey: { greeting: 'hi', retries: 3 },
    })
  })
})
