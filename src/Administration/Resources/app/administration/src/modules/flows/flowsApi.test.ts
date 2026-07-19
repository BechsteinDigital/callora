import { describe, it, expect, vi } from 'vitest'
import { flowsApi } from './flowsApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

const sampleInput = {
  name: 'Route to queue',
  triggerEvent: 'call.received',
  conditions: null,
  actions: [{ type: 'enqueue' }],
  isActive: true,
  priority: 100,
}

describe('flowsApi', () => {
  it('lists flows for a workspace with a limit', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ items: [{ id: '1' }], total: 1, nextCursor: null }))
    globalThis.fetch = fetchMock
    const page = await flowsApi.list('workspace-a')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/flows?workspaceKey=workspace-a&limit=50')
    expect(page.items[0].id).toBe('1')
  })

  it('passes the cursor for the next page', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ items: [], total: 60, nextCursor: null }))
    globalThis.fetch = fetchMock
    await flowsApi.list('workspace-a', 'cur1')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/flows?workspaceKey=workspace-a&limit=50&cursor=cur1')
  })

  it('creates a flow via POST with the workspace query and parsed body', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ id: '1' }, 201))
    globalThis.fetch = fetchMock
    await flowsApi.create('workspace-a', sampleInput)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/flows?workspaceKey=workspace-a')
    expect(fetchMock.mock.calls[0][1].method).toBe('POST')
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    expect(body.name).toBe('Route to queue')
    expect(body.actions).toEqual([{ type: 'enqueue' }])
  })

  it('updates a flow via PUT with the id and workspace query', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ id: 'a b' }))
    globalThis.fetch = fetchMock
    await flowsApi.update('workspace-a', 'a b', sampleInput)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/flows/a%20b?workspaceKey=workspace-a')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
  })

  it('deletes a flow via DELETE with the id and workspace query', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await flowsApi.remove('workspace-a', 'a b')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/flows/a%20b?workspaceKey=workspace-a')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('surfaces the RFC 9457 problem detail on error', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'name and triggerEvent are required.' }, 400))
    await expect(flowsApi.create('workspace-a', sampleInput)).rejects.toThrow('name and triggerEvent are required.')
  })
})
