import { flushPromises, mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdminContext } from '@/core/auth/adminContext'
import { registerExtension, resetExtensions } from '@/core/extensions/registry'
import { resetWorkspaceContext } from '@/core/workspace/workspaceContext'
import ExtensionHostView from './ExtensionHostView.vue'

const { contextRef, listMock } = vi.hoisted(() => ({
  contextRef: {
    value: {
      userId: 'operator',
      displayName: null,
      email: null,
      roles: [],
      permissions: ['*'],
      scope: null,
      workspaceKey: null,
      tenantKey: null,
      isOperator: true,
    } as AdminContext,
  },
  listMock: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { pluginId: 'videoconference' } }),
}))
vi.mock('@/core/auth/authStore', () => ({
  useAuthStore: () => ({ context: contextRef }),
}))
vi.mock('@/modules/workspaces/workspacesApi', () => ({
  workspacesApi: { list: listMock },
}))

beforeEach(() => {
  resetExtensions()
  resetWorkspaceContext()
  listMock.mockReset().mockResolvedValue([
    { workspaceKey: 'video', displayName: 'Video' },
  ])
})

describe('ExtensionHostView', () => {
  it('passes the active workspace through the page context', async () => {
    const Page = defineComponent({
      props: ['ctx'],
      setup: (props) => () => h('span', String(props.ctx?.workspaceKey ?? 'missing')),
    })
    registerExtension('extension.page.videoconference', Page)

    const wrapper = mount(ExtensionHostView)
    await flushPromises()

    expect(wrapper.text()).toContain('video')
  })
})
