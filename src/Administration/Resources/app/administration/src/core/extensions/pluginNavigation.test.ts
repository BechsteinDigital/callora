import { describe, it, expect, afterEach, vi } from 'vitest'
import {
  loadPluginNavigation,
  usePluginNavigation,
  resetPluginNavigation,
  type PluginNavItem,
} from './pluginNavigation'

const SAMPLE: PluginNavItem[] = [
  { pluginId: 'communication', id: 'communication', label: 'Communication', to: '/extensions/communication', icon: 'i-lucide-phone-call', order: 35 },
]

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  resetPluginNavigation()
})

describe('pluginNavigation', () => {
  it('loads the server navigation into the shared list', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify(SAMPLE), { status: 200 })))

    await loadPluginNavigation()

    expect(usePluginNavigation().items.value).toEqual(SAMPLE)
  })

  it('fetches only once per session', async () => {
    const spy = vi.fn().mockResolvedValue(new Response('[]', { status: 200 }))
    vi.stubGlobal('fetch', spy)

    await loadPluginNavigation()
    await loadPluginNavigation()

    expect(spy).toHaveBeenCalledTimes(1)
  })

  it('fails safe: a non-ok response leaves the list empty', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 500 })))

    await loadPluginNavigation()

    expect(usePluginNavigation().items.value).toEqual([])
  })

  it('fails safe: a network error leaves the list empty', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')))

    await loadPluginNavigation()

    expect(usePluginNavigation().items.value).toEqual([])
  })
})
