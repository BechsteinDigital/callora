import { describe, expect, it } from 'vitest'
import { scanExtensionPoints } from './scan-extension-points'

describe('scanExtensionPoints', () => {
  it('finds a slot declared in a template', () => {
    expect(scanExtensionPoints('<ExtensionSlot name="users.list.toolbar" />', 'x.vue')).toEqual([
      { kind: 'slot', name: 'users.list.toolbar', file: 'x.vue' },
    ])
  })

  it('finds a slot that also passes a context', () => {
    expect(
      scanExtensionPoints('<ExtensionSlot name="users.detail.fields" :ctx="{ userId }" />', 'x.vue'),
    ).toEqual([{ kind: 'slot', name: 'users.detail.fields', file: 'x.vue' }])
  })

  it('finds a slot whose name attribute comes after other attributes', () => {
    expect(scanExtensionPoints('<ExtensionSlot :ctx="row" name="a.b" />', 'x.vue')).toEqual([
      { kind: 'slot', name: 'a.b', file: 'x.vue' },
    ])
  })

  it('finds a hook invoked with a literal name', () => {
    expect(scanExtensionPoints("await runHook('users.before-save', draft)", 'x.vue')).toEqual([
      { kind: 'hook', name: 'users.before-save', file: 'x.vue' },
    ])
  })

  it('records a template-literal hook as a pattern, because the value is only known at runtime', () => {
    expect(scanExtensionPoints('await runHook(`plugins.before-${verb}`, {})', 'x.vue')).toEqual([
      { kind: 'hook', name: 'plugins.before-*', file: 'x.vue', dynamic: true },
    ])
  })

  it('finds several points in one file and keeps them apart by kind', () => {
    const source = `<ExtensionSlot name="a.b" />\nawait runHook('a.before-save', {})`

    expect(scanExtensionPoints(source, 'x.vue')).toEqual([
      { kind: 'slot', name: 'a.b', file: 'x.vue' },
      { kind: 'hook', name: 'a.before-save', file: 'x.vue' },
    ])
  })

  it('deduplicates a point declared twice in one file', () => {
    expect(scanExtensionPoints('<ExtensionSlot name="a.b" /><ExtensionSlot name="a.b" />', 'x.vue')).toHaveLength(1)
  })

  it('ignores the ExtensionSlot component definition itself', () => {
    expect(scanExtensionPoints('defineProps<{ name: string; ctx?: unknown }>()', 'ExtensionSlot.vue')).toEqual([])
  })

  it('ignores a runHook definition rather than treating it as a call site', () => {
    expect(scanExtensionPoints('export async function runHook<T>(name: string, payload: T) {}', 'hooks.ts')).toEqual([])
  })

  it('returns nothing for a file with no points', () => {
    expect(scanExtensionPoints('export const x = 1', 'x.ts')).toEqual([])
  })

  it('ignores a point mentioned in a block comment, so documentation is not a contract', () => {
    const source = `/**\n * Call it like runHook('users.before-save', draft).\n */\nexport const x = 1`

    expect(scanExtensionPoints(source, 'docs.ts')).toEqual([])
  })

  it('ignores a point mentioned in a line comment', () => {
    expect(scanExtensionPoints("// <ExtensionSlot name=\"a.b\" /> is how it looks", 'x.ts')).toEqual([])
  })

  it('ignores a point mentioned in an HTML comment inside a template', () => {
    expect(scanExtensionPoints('<!-- <ExtensionSlot name="a.b" /> -->', 'x.vue')).toEqual([])
  })

  it('still finds a real point in a file that also documents one', () => {
    const source = `// runHook('a.documented') is an example\n<ExtensionSlot name="a.real" />`

    expect(scanExtensionPoints(source, 'x.vue')).toEqual([{ kind: 'slot', name: 'a.real', file: 'x.vue' }])
  })
})
