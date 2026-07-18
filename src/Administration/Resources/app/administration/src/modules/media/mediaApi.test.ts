import { describe, it, expect, vi } from 'vitest'
import { mediaApi } from './mediaApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('mediaApi', () => {
  it('lists a workspace media page and unwraps its items', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(
      respond({ items: [{ id: '1', fileName: 'a.png' }], total: 1, nextCursor: null }),
    )
    globalThis.fetch = fetchMock
    const items = await mediaApi.list('ws1')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/media?workspaceKey=ws1')
    expect(items).toEqual([{ id: '1', fileName: 'a.png' }])
  })

  it('adds the folder filter when given', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ items: [] }))
    globalThis.fetch = fetchMock
    await mediaApi.list('ws1', 'logos')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/media?workspaceKey=ws1&folder=logos')
  })

  it('uploads a multipart form with the file field', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond({ id: '1' }, 201))
    globalThis.fetch = fetchMock
    const file = new File(['x'], 'logo.png', { type: 'image/png' })
    await mediaApi.upload('ws1', file, 'branding')

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/media?workspaceKey=ws1&folder=branding')
    expect(init.method).toBe('POST')
    expect(init.body).toBeInstanceOf(FormData)
    expect((init.body as FormData).get('file')).toBe(file)
  })

  it('deletes by id within the workspace', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond(null, 204))
    globalThis.fetch = fetchMock
    await mediaApi.remove('ws1', 'abc')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/media/abc?workspaceKey=ws1')
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('builds a workspace-scoped content url', () => {
    expect(mediaApi.contentUrl('ws1', 'abc')).toBe('/api/media/abc/content?workspaceKey=ws1')
  })
})
