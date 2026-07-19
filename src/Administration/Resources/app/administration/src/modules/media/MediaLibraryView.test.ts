import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import MediaLibraryView from './MediaLibraryView.vue'
import type { AdminContext } from '@/core/auth/adminContext'
import type { MediaItem } from './mediaApi'
import { registerHook, resetHooks } from '@/core/extensions/hooks'
import { resetServices } from '@/core/extensions/services'
import { resetWorkspaceContext, useWorkspaceContext } from '@/core/workspace/workspaceContext'

const { listMock, uploadMock, removeMock, listWorkspacesMock, contextRef } = vi.hoisted(() => ({
  listMock: vi.fn(),
  uploadMock: vi.fn(),
  removeMock: vi.fn(),
  listWorkspacesMock: vi.fn(),
  contextRef: { value: null as AdminContext | null },
}))

vi.mock('./mediaApi', () => ({
  MEDIA_ALLOWED_CONTENT_TYPES: ['audio/mpeg', 'image/png'],
  MEDIA_MAX_SIZE_BYTES: 25 * 1024 * 1024,
  mediaApi: {
    list: listMock,
    upload: uploadMock,
    remove: removeMock,
    contentUrl: (ws: string, id: string) => `/api/media/${id}/content?workspaceKey=${ws}`,
  },
}))
vi.mock('@/modules/workspaces/workspacesApi', () => ({ workspacesApi: { list: listWorkspacesMock } }))
vi.mock('@/core/auth/authStore', () => ({ useAuthStore: () => ({ context: contextRef }) }))

function ctx(permissions: string[], workspaceKey: string | null): AdminContext {
  return {
    userId: 'u',
    displayName: null,
    email: null,
    roles: [],
    permissions,
    scope: null,
    workspaceKey,
    isOperator: workspaceKey === null,
  }
}

function item(over: Partial<MediaItem>): MediaItem {
  return {
    id: 'm1',
    workspaceKey: 'ws1',
    fileName: 'logo.png',
    contentType: 'image/png',
    sizeBytes: 1024,
    folder: 'branding',
    createdBy: null,
    createdAtUtc: '',
    ...over,
  }
}

function selectFile(wrapper: VueWrapper, file: File): Promise<void> {
  const input = wrapper.find('input[type="file"]')
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
  return input.trigger('change')
}

beforeEach(() => {
  listMock.mockReset().mockResolvedValue([item({ id: 'm1', contentType: 'image/png' })])
  uploadMock.mockReset().mockResolvedValue(item({}))
  removeMock.mockReset().mockResolvedValue(undefined)
  listWorkspacesMock.mockReset().mockResolvedValue([{ workspaceKey: 'wsA', displayName: 'A' }])
  resetHooks()
  resetServices()
  resetWorkspaceContext()
})

describe('MediaLibraryView', () => {
  it('uses the fixed workspace from context without a picker', async () => {
    contextRef.value = ctx(['media.read'], 'ws1')
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    expect(listMock).toHaveBeenCalledWith('ws1')
    expect(wrapper.find('select[name="workspace"]').exists()).toBe(false)
    // image item renders an inline preview from the content url
    expect(wrapper.find('img').attributes('src')).toBe('/api/media/m1/content?workspaceKey=ws1')
  })

  it('lists media for the operator’s active workspace from the global context', async () => {
    contextRef.value = ctx(['*'], null)
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    // The global context (not an in-view picker) loads the operator's workspaces
    // and the view scopes to the active one.
    expect(listWorkspacesMock).toHaveBeenCalled()
    expect(wrapper.find('select[name="workspace"]').exists()).toBe(false)
    expect(listMock).toHaveBeenCalledWith('wsA')
  })

  it('hides upload and delete without media.manage', async () => {
    contextRef.value = ctx(['media.read'], 'ws1')
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    expect(wrapper.find('form.upload').exists()).toBe(false)
    expect(wrapper.find('.link-danger').exists()).toBe(false)
  })

  it('uploads a selected file and reloads', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    const file = new File(['x'], 'logo.png', { type: 'image/png' })
    await selectFile(wrapper, file)
    await wrapper.find('form.upload').trigger('submit.prevent')
    await flushPromises()

    expect(uploadMock).toHaveBeenCalledWith('ws1', file, undefined)
    expect(listMock).toHaveBeenCalledTimes(2) // initial + reload
  })

  it('rejects a disallowed content type client-side', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    await selectFile(wrapper, new File(['x'], 'notes.txt', { type: 'text/plain' }))
    await wrapper.find('form.upload').trigger('submit.prevent')
    await flushPromises()

    expect(uploadMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('nicht erlaubt')
  })

  it('aborts upload when a before-upload hook cancels', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    registerHook('media.before-upload', (h) => h.cancel('Upload gesperrt'))
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    await selectFile(wrapper, new File(['x'], 'logo.png', { type: 'image/png' }))
    await wrapper.find('form.upload').trigger('submit.prevent')
    await flushPromises()

    expect(uploadMock).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Upload gesperrt')
  })

  it('deletes an item after confirmation and reloads', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMock).toHaveBeenCalledWith('ws1', 'm1')
    expect(listMock).toHaveBeenCalledTimes(2)
  })

  it('does not delete when confirmation is dismissed', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(removeMock).not.toHaveBeenCalled()
  })

  it('reloads media when the global workspace switches', async () => {
    contextRef.value = ctx(['*'], null)
    listWorkspacesMock.mockResolvedValue([
      { workspaceKey: 'wsA', displayName: 'A' },
      { workspaceKey: 'wsB', displayName: 'B' },
    ])
    mount(MediaLibraryView)
    await flushPromises()

    // The topbar switcher writes the shared context; the view reacts.
    useWorkspaceContext().setActive('wsB')
    await flushPromises()

    expect(listMock).toHaveBeenLastCalledWith('wsB')
  })

  it('renders an inline audio player for audio items', async () => {
    contextRef.value = ctx(['media.read'], 'ws1')
    listMock.mockResolvedValue([item({ id: 'a1', contentType: 'audio/mpeg', fileName: 'ansage.mp3' })])
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    expect(wrapper.find('audio').attributes('src')).toBe('/api/media/a1/content?workspaceKey=ws1')
  })

  it('runs the after-upload hook and applies a before-upload folder mutation', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    const seen: unknown[] = []
    registerHook<{ folder: string }>('media.before-upload', (h) => {
      h.payload.folder = 'overridden'
    })
    registerHook('media.after-upload', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    const file = new File(['x'], 'logo.png', { type: 'image/png' })
    await selectFile(wrapper, file)
    await wrapper.find('form.upload').trigger('submit.prevent')
    await flushPromises()

    expect(uploadMock).toHaveBeenCalledWith('ws1', file, 'overridden')
    expect(seen).toEqual([{ workspaceKey: 'ws1', fileName: 'logo.png' }])
  })

  it('runs the after-delete hook on a successful delete', async () => {
    contextRef.value = ctx(['media.read', 'media.manage'], 'ws1')
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const seen: unknown[] = []
    registerHook('media.after-delete', (h) => {
      seen.push(h.payload)
    })
    const wrapper = mount(MediaLibraryView)
    await flushPromises()

    await wrapper.find('.link-danger').trigger('click')
    await flushPromises()

    expect(seen).toEqual([{ workspaceKey: 'ws1', id: 'm1' }])
  })
})
