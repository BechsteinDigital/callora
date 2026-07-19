import { describe, it, expect, vi } from 'vitest'
import { webhooksApi } from './webhooksApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('webhooksApi', () => {
  it('lists the first page with a limit', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ items: [{ id: '1' }], total: 1, nextCursor: null }))
    globalThis.fetch = fetchMock
    const page = await webhooksApi.list()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/webhooks?limit=50')
    expect(page.items[0].id).toBe('1')
  })

  it('passes the cursor for the next page', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ items: [], total: 60, nextCursor: null }))
    globalThis.fetch = fetchMock
    await webhooksApi.list('cur123')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/webhooks?limit=50&cursor=cur123')
  })

  it('creates a subscription via POST with the full input', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ id: '1' }, 201))
    globalThis.fetch = fetchMock
    await webhooksApi.create({
      eventName: 'workspace.created',
      targetUrl: 'https://hook.example.de',
      secret: 's3cr3t',
      workspaceKey: null,
      includeSensitiveData: false,
    })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/webhooks')
    expect(fetchMock.mock.calls[0][1].method).toBe('POST')
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    expect(body.eventName).toBe('workspace.created')
    expect(body.secret).toBe('s3cr3t')
    expect(body.workspaceKey).toBeNull()
  })

  it('toggles activation via the activation route with the isActive flag', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await webhooksApi.setActive('a b', false)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/webhooks/a%20b/activation?isActive=false')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
  })

  it('deletes via DELETE with an encoded id', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await webhooksApi.remove('a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/webhooks/a%20b')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('surfaces the RFC 9457 problem detail on error', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'targetUrl must be absolute.' }, 400))
    await expect(
      webhooksApi.create({
        eventName: 'x',
        targetUrl: 'bad',
        secret: 's',
        workspaceKey: null,
        includeSensitiveData: false,
      }),
    ).rejects.toThrow('targetUrl must be absolute.')
  })
})
