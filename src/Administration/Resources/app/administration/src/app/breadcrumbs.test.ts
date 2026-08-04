import { describe, expect, it } from 'vitest'
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import { breadcrumbsFor } from './breadcrumbs'

function route(meta: Record<string, unknown>): RouteLocationNormalizedLoaded {
  return { meta } as unknown as RouteLocationNormalizedLoaded
}

describe('breadcrumbsFor', () => {
  it('renders a single step for a top-level page', () => {
    expect(breadcrumbsFor(route({ title: 'Benutzer' }))).toEqual([{ label: 'Benutzer' }])
  })

  it('puts the section in front of a detail page and links back to it', () => {
    const trail = breadcrumbsFor(route({ title: 'Benutzer bearbeiten', parent: { label: 'Benutzer', to: '/users' } }))

    expect(trail).toEqual([{ label: 'Benutzer', to: '/users' }, { label: 'Benutzer bearbeiten' }])
  })

  it('leaves the current page without a target — it is where you already are', () => {
    const trail = breadcrumbsFor(route({ title: 'Neue Rolle', parent: { label: 'Rollen', to: '/roles' } }))

    expect(trail[trail.length - 1].to).toBeUndefined()
  })

  it('falls back to the dashboard label when a route declares no title', () => {
    expect(breadcrumbsFor(route({}))).toEqual([{ label: 'Übersicht' }])
  })

  it('ignores a malformed parent instead of rendering a broken crumb', () => {
    expect(breadcrumbsFor(route({ title: 'Detail', parent: { label: 'Nur ein Label' } }))).toEqual([
      { label: 'Detail' },
    ])
    expect(breadcrumbsFor(route({ title: 'Detail', parent: 'Benutzer' }))).toEqual([{ label: 'Detail' }])
  })

  it('ignores a non-string title', () => {
    expect(breadcrumbsFor(route({ title: 42 }))).toEqual([{ label: 'Übersicht' }])
  })
})
