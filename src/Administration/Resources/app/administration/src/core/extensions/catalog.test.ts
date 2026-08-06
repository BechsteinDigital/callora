import { describe, expect, it } from 'vitest'
import catalog from './catalog.json'

/**
 * The catalog is generated from the shell and committed; CI regenerates it and fails on a diff.
 * These tests guard what a diff cannot: that the generated content is actually usable — every
 * point named by convention, attributed to a file, and covering the points we know exist.
 */
describe('extension point catalog', () => {
  it('contains the slots the shell actually renders', () => {
    const names = catalog.slots.map((slot) => slot.name)

    expect(names).toContain('users.list.toolbar')
    expect(names).toContain('users.detail.fields')
    expect(names).toContain('dashboard.metrics')
    expect(names).toContain('config.fields')
  })

  it('contains the hooks the shell actually runs', () => {
    const names = catalog.hooks.map((hook) => hook.name)

    expect(names).toContain('users.before-save')
    expect(names).toContain('users.after-save')
    expect(names).toContain('media.before-upload')
  })

  it('records a runtime-assembled hook family as a pattern rather than dropping it', () => {
    const dynamic = catalog.hooks.filter((hook) => hook.dynamic)

    expect(dynamic.length).toBeGreaterThan(0)
    for (const hook of dynamic) {
      expect(hook.name.endsWith('*')).toBe(true)
    }
  })

  it('names every point in the {module}.{…} convention, so the catalog stays navigable', () => {
    for (const point of [...catalog.slots, ...catalog.hooks]) {
      expect(point.name, `Punkt "${point.name}" in ${point.file}`).toMatch(
        /^[a-z][a-z0-9-]*(\.[a-z0-9-]+)+\*?$/,
      )
    }
  })

  it('attributes every point to the file that declares it', () => {
    for (const point of [...catalog.slots, ...catalog.hooks]) {
      expect(point.file).toMatch(/\.(vue|ts)$/)
    }
  })

  it('lists no point twice within one kind', () => {
    for (const points of [catalog.slots, catalog.hooks]) {
      const names = points.map((point) => point.name)
      expect(new Set(names).size, `Duplikate: ${names.filter((n, i) => names.indexOf(n) !== i)}`).toBe(names.length)
    }
  })

  it('is not empty — an empty catalog would mean the scanner silently matched nothing', () => {
    expect(catalog.slots.length).toBeGreaterThan(20)
    expect(catalog.hooks.length).toBeGreaterThan(20)
  })
})
