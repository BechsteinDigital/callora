import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import WebhooksListView from './WebhooksListView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { WebhookSubscription, WebhookSubscriptionsPage } from './webhooksApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'

const { listMock, createMock, setActiveMock, removeMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  createMock: vi.fn(),
  setActiveMock: vi.fn(),
  removeMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./webhooksApi', () => ({
  WEBHOOKS_PAGE_SIZE: 50,
  webhooksApi: { list: listMock, create: createMock, setActive: setActiveMock, remove: removeMock },
}))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

// The confirm dialog is a promise-based store now, not window.confirm — mock it so
// each test can decide what the operator answers.
const { confirmMock } = vi.hoisted(() => ({ confirmMock: vi.fn() }))
vi.mock('@/core/feedback/confirm', () => ({ confirm: confirmMock }))

function ctx(permissions: string[]): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey: null,
    isOperator: false,
  }
}

function hook(over: Partial<WebhookSubscription>): WebhookSubscription {
  return {
    id: 'w1',
    workspaceKey: 'acme',
    eventName: 'workspace.created',
    targetUrl: 'https://hook.example.de',
    isActive: true,
    includeSensitiveData: false,
    createdAtUtc: '',
    ...over,
  }
}

function page(items: WebhookSubscription[], nextCursor: string | null = null, total = items.length): WebhookSubscriptionsPage {
  return { items, total, nextCursor }
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue(page([hook({})]))
  confirmMock.mockReset().mockResolvedValue(true)
  createMock.mockReset().mockResolvedValue(hook({}))
  setActiveMock.mockReset().mockResolvedValue(undefined)
  removeMock.mockReset().mockResolvedValue(undefined)
  resetHooks()
  resetServices()
})

describe('WebhooksListView', () => {
  it('lists webhooks with event, target and status', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('workspace.created')
    expect(text).toContain('https://hook.example.de')
    expect(text).toContain('acme')
    expect(text).toContain('Aktiv')
  })

  it('hides the create form and row actions without webhook.manage', async () => {
    contextRef.value = ctx(['webhook.read'])
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    expect(wrapper.find('form.webhooks__form').exists()).toBe(false)
    expect(wrapper.find('.is-danger-ghost').exists()).toBe(false)
  })

  it('creates a webhook from the form (empty workspace → null) and reloads', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.find('input[name="eventName"]').setValue('user.created')
    await wrapper.find('input[name="targetUrl"]').setValue('https://x.example.de')
    await wrapper.find('input[name="secret"]').setValue('topsecret')
    await wrapper.find('form.webhooks__form').trigger('submit')
    await flushPromises()

    expect(createMock).toHaveBeenCalledTimes(1)
    const input = createMock.mock.calls[0][0]
    expect(input).toEqual({
      eventName: 'user.created',
      targetUrl: 'https://x.example.de',
      secret: 'topsecret',
      workspaceKey: null,
      includeSensitiveData: false,
    })
    expect(listMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('keeps the secret out of the before-create hook payload', async () => {
    contextRef.value = ctx(['*'])
    let seenPayload: Record<string, unknown> | null = null
    registerHook('webhooks.before-create', (h) => {
      seenPayload = h.payload as Record<string, unknown>
    })
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.find('input[name="eventName"]').setValue('user.created')
    await wrapper.find('input[name="targetUrl"]').setValue('https://x.example.de')
    await wrapper.find('input[name="secret"]').setValue('topsecret')
    await wrapper.find('form.webhooks__form').trigger('submit')
    await flushPromises()

    // The hook sees the draft but never the raw signing secret…
    expect(seenPayload).not.toBeNull()
    expect(seenPayload!).not.toHaveProperty('secret')
    // …while the API call still carries it.
    expect(createMock.mock.calls[0][0].secret).toBe('topsecret')
  })

  it('does not submit without event, url and secret', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.find('input[name="eventName"]').setValue('user.created')
    // targetUrl and secret left empty
    await wrapper.find('form.webhooks__form').trigger('submit')
    await flushPromises()

    expect(createMock).not.toHaveBeenCalled()
  })

  it('toggles activation for a webhook', async () => {
    contextRef.value = ctx(['*'])
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.findAll('button.is-ghost').find((b) => b.text() === 'Deaktivieren')!.trigger('click')
    await flushPromises()

    expect(setActiveMock).toHaveBeenCalledWith('w1', false)
  })

  it('deletes after confirmation and runs the after-delete hook', async () => {
    contextRef.value = ctx(['*'])
    confirmMock.mockResolvedValue(true)
    const seen: unknown[] = []
    registerHook('webhooks.after-delete', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeMock).toHaveBeenCalledWith('w1')
    expect(seen).toEqual([{ id: 'w1' }])
  })

  it('does not delete when the confirm dialog is dismissed', async () => {
    contextRef.value = ctx(['*'])
    confirmMock.mockResolvedValue(false)
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.find('.is-danger-ghost').trigger('click')
    await flushPromises()

    expect(removeMock).not.toHaveBeenCalled()
  })

  it('aborts create when a before-create hook cancels', async () => {
    contextRef.value = ctx(['*'])
    registerHook('webhooks.before-create', (h) => h.cancel('gesperrt'))
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    await wrapper.find('input[name="eventName"]').setValue('user.created')
    await wrapper.find('input[name="targetUrl"]').setValue('https://x.example.de')
    await wrapper.find('input[name="secret"]').setValue('topsecret')
    await wrapper.find('form.webhooks__form').trigger('submit')
    await flushPromises()

    expect(createMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('gesperrt')
  })

  it('appends the next page via the cursor', async () => {
    contextRef.value = ctx(['*'])
    listMock
      .mockResolvedValueOnce(page([hook({ id: 'w1', eventName: 'a.evt' })], 'cursor-1', 2))
      .mockResolvedValueOnce(page([hook({ id: 'w2', eventName: 'b.evt' })], null, 2))
    const wrapper = mount(WebhooksListView)
    await flushPromises()

    expect(wrapper.text()).not.toContain('b.evt')
    await wrapper.findAll('button').find((b) => b.text().includes('Mehr laden'))!.trigger('click')
    await flushPromises()

    expect(listMock).toHaveBeenLastCalledWith('cursor-1')
    expect(wrapper.text()).toContain('a.evt')
    expect(wrapper.text()).toContain('b.evt')
  })
})
