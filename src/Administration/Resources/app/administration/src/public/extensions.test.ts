import { beforeEach, describe, expect, it, vi } from 'vitest'
import { h } from 'vue'
import {
  registerExtension,
  registerHook,
  registerPage,
  registerService,
  resolveAdminApi,
  t,
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

describe('t', () => {
  beforeEach(() => {
    delete (globalThis as Global).CalloraAdmin
  })

  it('returns the shell translation when the key is known', () => {
    ;(globalThis as Global).CalloraAdmin = {
      translate: (key: string, fallback: string) => (key === 'pbx.hello' ? 'Hallo' : fallback),
    }

    expect(t('pbx.hello', 'Hello')).toBe('Hallo')
  })

  it('falls back to the text passed in when the key is unknown', () => {
    // What makes this adoptable one line at a time: until a key exists, the fallback shows — never
    // the key itself. A screen displaying `pbx.person.blocked` to an operator is worse than an
    // untranslated one.
    ;(globalThis as Global).CalloraAdmin = {
      translate: (_key: string, fallback: string) => fallback,
    }

    expect(t('pbx.unknown', 'Fallback')).toBe('Fallback')
  })

  it('falls back without warning when the shell is absent', () => {
    // The only function here that does not warn: every other one registers something and can say
    // "that did not happen". This one has to return a string, and the fallback is a correct answer
    // rather than a degraded one — a warning per rendered label would drown a test run in noise.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    delete (globalThis as Record<string, unknown>).CalloraAdmin

    expect(t('pbx.anything', 'Fallback')).toBe('Fallback')
    expect(warn).not.toHaveBeenCalled()

    warn.mockRestore()
  })
})
