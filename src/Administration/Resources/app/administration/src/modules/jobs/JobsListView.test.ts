import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import JobsListView from './JobsListView.vue'
import type { Job } from './jobsApi'
import { resetServices } from '@/core/extensions/services'

const { listMock } = vi.hoisted(() => ({ listMock: vi.fn() }))

vi.mock('./jobsApi', () => ({ jobsApi: { list: listMock } }))

function job(over: Partial<Job>): Job {
  return {
    id: 'j1',
    jobType: 'plugin.sync',
    status: 'Completed',
    workspaceKey: 'acme',
    attemptCount: 1,
    maxAttempts: 3,
    scheduledAtUtc: null,
    createdAtUtc: '2026-07-19T10:00:00Z',
    startedAtUtc: '2026-07-19T10:00:01Z',
    completedAtUtc: '2026-07-19T10:00:02Z',
    lastError: null,
    ...over,
  }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([job({})])
  resetServices()
})

describe('JobsListView', () => {
  it('lists jobs with type, status and attempts', async () => {
    const wrapper = mount(JobsListView)
    await flushPromises()

    expect(listMock).toHaveBeenCalledWith(25) // default limit
    const text = wrapper.text()
    expect(text).toContain('plugin.sync')
    expect(text).toContain('Completed')
    expect(text).toContain('1 / 3')
    expect(text).toContain('acme')
  })

  it('renders an em dash in the workspace, completed and error cells when those are null', async () => {
    listMock.mockResolvedValueOnce([job({ workspaceKey: null, completedAtUtc: null, lastError: null })])
    const wrapper = mount(JobsListView)
    await flushPromises()

    // Columns: Typ | Status | Workspace | Versuche | Erstellt | Abgeschlossen | Fehler
    const cells = wrapper.findAll('tbody td')
    expect(cells[2].text()).toBe('—') // workspace
    expect(cells[5].text()).toBe('—') // completed
    expect(cells[6].text()).toBe('—') // last error
  })

  it('reloads on the refresh action', async () => {
    const wrapper = mount(JobsListView)
    await flushPromises()

    // The refresh action is the only button on the page (the limit is a <select>).
    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(listMock).toHaveBeenCalledTimes(2)
  })

  it('reloads with the chosen limit', async () => {
    const wrapper = mount(JobsListView)
    await flushPromises()

    await wrapper.find('select[name="jobLimit"]').setValue('100')
    await flushPromises()

    expect(listMock).toHaveBeenLastCalledWith(100)
  })

  it('shows the problem detail on a failed load', async () => {
    listMock.mockReset().mockRejectedValueOnce(new Error('Forbidden.'))
    const wrapper = mount(JobsListView)
    await flushPromises()

    expect(wrapper.text()).toContain('Forbidden.')
  })
})
