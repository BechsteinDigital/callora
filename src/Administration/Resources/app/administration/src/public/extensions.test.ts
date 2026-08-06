import { beforeEach, describe, expect, it, vi } from 'vitest'
import { h } from 'vue'
import {
  registerExtension,
  registerHook,
  registerPage,
  registerService,
  resolveAdminApi,
} from './extensions'

type Global = Record<string, unknown>
const Dummy = { setup: () => () => h('div') }

describe('admin registration API', () => {
  beforeEach(() => {
    delete (globalThis as Global).CalloraAdmin
  })

  it('forwards a slot registration to the shell', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerExtension: spy }

    registerExtension('users.list.toolbar', Dummy, 10)

    expect(spy).toHaveBeenCalledWith('users.list.toolbar', Dummy, 10)
  })

  it('forwards a hook registration to the shell', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerHook: spy }
    const handler = (): void => {}

    registerHook('users.before-save', handler)

    expect(spy).toHaveBeenCalledWith('users.before-save', handler, undefined)
  })

  it('forwards a service override to the shell', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerService: spy }
    const impl = { list: async () => [] }

    registerService('usersApi', impl, { priority: 5 })

    expect(spy).toHaveBeenCalledWith('usersApi', impl, { priority: 5 })
  })

  it('registers a full plugin page under the slot the shell routes to', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerExtension: spy }

    registerPage('communication', Dummy)

    expect(spy).toHaveBeenCalledWith('extension.page.communication', Dummy, undefined)
  })

  it('warns instead of throwing when the shell is absent, so a plugin never breaks it', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    expect(() => registerExtension('users.list.toolbar', Dummy)).not.toThrow()
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('admin shell not initialised'))

    warn.mockRestore()
  })

  it('names the missing registration in the warning, so a silent plugin is diagnosable', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    registerPage('communication', Dummy)

    expect(warn).toHaveBeenCalledWith(expect.stringContaining('communication'))

    warn.mockRestore()
  })

  it('returns undefined from resolveAdminApi before the shell installed it', () => {
    expect(resolveAdminApi()).toBeUndefined()
  })

  it('resolves the shell API once it is installed', () => {
    const api = { registerExtension: vi.fn() }
    ;(globalThis as Global).CalloraAdmin = api

    expect(resolveAdminApi()).toBe(api)
  })

  // The point of the generated catalog: a wrong name must not compile. The suppression comments
  // below are checked by vue-tsc during the build — should the compiler ever stop rejecting
  // those calls, the unused suppression is itself an error and the build fails. Either way the
  // guarantee holds.
  //
  // (Spelled out rather than quoted: the compiler reads that directive in ANY comment, so naming
  // it here would have applied it to the following line.)
  it('rejects a slot name the shell does not render', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerExtension: spy }

    // @ts-expect-error 'users.list.toolbarr' is not a slot the shell renders
    registerExtension('users.list.toolbarr', Dummy)

    // It still forwards at runtime — the guarantee is a compile-time one, and a plugin built
    // against an older catalog must degrade rather than crash.
    expect(spy).toHaveBeenCalled()
  })

  it('rejects a hook name the shell does not run', () => {
    ;(globalThis as Global).CalloraAdmin = { registerHook: vi.fn() }

    // @ts-expect-error 'users.before-safe' is a typo for 'users.before-save'
    registerHook('users.before-safe', () => {})
  })
})
