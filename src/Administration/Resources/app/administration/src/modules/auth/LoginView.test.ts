import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import LoginView from './LoginView.vue'

// useAuthStore returns a fresh object per call, so mock the module to expose a
// stable login spy; useRouter is stubbed to observe navigation.
const { loginMock, reloadMock } = vi.hoisted(() => ({ loginMock: vi.fn(), reloadMock: vi.fn() }))
vi.mock('@/core/auth/authStore', () => ({
  useAuthStore: () => ({ login: loginMock, context: { value: null } }),
}))

beforeEach(() => {
  loginMock.mockReset()
  reloadMock.mockReset()
})

describe('LoginView', () => {
  it('meldet an und lädt die Shell neu, statt nur zu navigieren', async () => {
    // Die Plugin-Bundles werden beim Bootstrap geladen, und der lief hier ohne Sitzung — es gibt
    // also noch keine. Eine reine Navigation zeigte eine Oberfläche ohne jede Plugin-Seite, und
    // niemand erführe, warum.
    loginMock.mockResolvedValue(true)
    const wrapper = mount(LoginView, { props: { reload: reloadMock } })

    await wrapper.find('input[name="login"]').setValue('root')
    await wrapper.find('input[name="password"]').setValue('pass')
    await wrapper.find('form').trigger('submit.prevent')
    await Promise.resolve()

    expect(loginMock).toHaveBeenCalledWith('root', 'pass', null)
    expect(reloadMock).toHaveBeenCalled()
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
    expect(reloadMock).not.toHaveBeenCalled()
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
