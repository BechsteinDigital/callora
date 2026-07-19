import { describe, it, expect, vi } from 'vitest'
import { jobsApi } from './jobsApi'

function respond(body: unknown, status = 200): Response {
  return new Response(body === null ? null : JSON.stringify(body), { status })
}

describe('jobsApi', () => {
  it('lists jobs without a limit query', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([{ id: '1', jobType: 'sync', status: 'Pending' }]))
    globalThis.fetch = fetchMock
    const jobs = await jobsApi.list()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/jobs')
    expect(jobs[0].jobType).toBe('sync')
  })

  it('passes the limit as a query parameter', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(respond([]))
    globalThis.fetch = fetchMock
    await jobsApi.list(50)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/jobs?limit=50')
  })

  it('surfaces the RFC 9457 problem detail on error', async () => {
    globalThis.fetch = vi.fn().mockResolvedValueOnce(respond({ detail: 'Forbidden.' }, 403))
    await expect(jobsApi.list()).rejects.toThrow('Forbidden.')
  })
})
