import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import LoginView from './LoginView.vue'

// useAuthStore returns a fresh object per call, so mock the module to expose a
// stable login spy; useRouter is stubbed to observe navigation.
const { loginMock, pushMock } = vi.hoisted(() => ({ loginMock: vi.fn(), pushMock: vi.fn() }))
vi.mock('@/core/auth/authStore', () => ({
  useAuthStore: () => ({ login: loginMock, context: { value: null } }),
}))
vi.mock('vue-router', () => ({ useRouter: () => ({ push: pushMock }) }))

beforeEach(() => {
  loginMock.mockReset()
  pushMock.mockReset()
})

describe('LoginView', () => {
  it('calls login with the entered credentials and navigates on success', async () => {
    loginMock.mockResolvedValue(true)
    const wrapper = mount(LoginView)

    await wrapper.find('input[name="login"]').setValue('root')
    await wrapper.find('input[name="password"]').setValue('pass')
    await wrapper.find('form').trigger('submit.prevent')
    await Promise.resolve()

    expect(loginMock).toHaveBeenCalledWith('root', 'pass', null)
    expect(pushMock).toHaveBeenCalledWith('/')
  })

  it('shows an error and stays on the page when login fails', async () => {
    loginMock.mockResolvedValue(false)
    const wrapper = mount(LoginView)

    await wrapper.find('input[name="login"]').setValue('x')
    await wrapper.find('input[name="password"]').setValue('y')
    await wrapper.find('form').trigger('submit.prevent')
    await Promise.resolve()
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('fehlgeschlagen')
    expect(pushMock).not.toHaveBeenCalled()
  })

  it('passes the entered workspace key through to login', async () => {
    loginMock.mockResolvedValue(true)
    const wrapper = mount(LoginView)

    await wrapper.find('input[name="login"]').setValue('alice')
    await wrapper.find('input[name="password"]').setValue('pass-1')
    await wrapper.find('input[name="workspaceKey"]').setValue('workspace-a')
    await wrapper.find('form').trigger('submit.prevent')
    await Promise.resolve()

    expect(loginMock).toHaveBeenCalledWith('alice', 'pass-1', 'workspace-a')
  })
})
