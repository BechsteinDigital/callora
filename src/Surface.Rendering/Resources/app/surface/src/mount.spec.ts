import { describe, it, expect } from 'vitest'
import { defineComponent, h, nextTick } from 'vue'
import { mountSurface } from './mount'
import { createSurfaceRegistry } from './surface-registry'
import type { SurfaceContext } from './surface-context'

// A view that renders its id plus the workspace it received, so tests can assert both
// that it mounted in the right place and that it got the resolved context.
function probe(id: string) {
  return defineComponent({
    props: { context: { type: Object, required: true } },
    setup: (props) => () =>
      h('span', { 'data-testid': id }, (props.context as SurfaceContext).workspaceKey),
  })
}

describe('mountSurface — app mode', () => {
  it('mounts the whole app into #callora-app and renders registered views', () => {
    document.body.innerHTML = '<div id="callora-app" data-workspace="acme" data-surface="portal"></div>'
    const registry = createSurfaceRegistry()
    registry.registerView({ id: 'home', component: probe('home') })

    mountSurface(registry, document)

    expect(document.querySelector('[data-testid="home"]')?.textContent).toBe('acme')
  })
})

describe('mountSurface — islands mode', () => {
  it('mounts only the matching view into each data-callora-island placeholder', () => {
    document.body.innerHTML =
      '<main data-workspace="acme" data-surface="portal">' +
      '  <div data-callora-island="voip.button"></div>' +
      '  <div data-callora-island="voip.status"></div>' +
      '</main>'
    const registry = createSurfaceRegistry()
    registry.registerView({ id: 'voip.button', component: probe('voip.button') })
    // voip.status is intentionally NOT registered → its island stays empty, no crash.

    mountSurface(registry, document)

    expect(document.querySelector('[data-testid="voip.button"]')?.textContent).toBe('acme')
    const statusIsland = document.querySelector('[data-callora-island="voip.status"]')
    expect(statusIsland?.textContent?.trim()).toBe('')
  })

  it('renders an island whose view registers AFTER mounting (reactive, late plugin)', async () => {
    document.body.innerHTML =
      '<main data-workspace="acme"><div data-callora-island="late.view"></div></main>'
    const registry = createSurfaceRegistry()

    mountSurface(registry, document)
    expect(document.querySelector('[data-testid="late.view"]')).toBeNull()

    registry.registerView({ id: 'late.view', component: probe('late.view') })
    await nextTick()

    expect(document.querySelector('[data-testid="late.view"]')?.textContent).toBe('acme')
  })

  it('resolves context from the nearest ancestor and falls back to default', () => {
    document.body.innerHTML = '<div data-callora-island="loose.view"></div>'
    const registry = createSurfaceRegistry()
    registry.registerView({ id: 'loose.view', component: probe('loose.view') })

    mountSurface(registry, document)

    expect(document.querySelector('[data-testid="loose.view"]')?.textContent).toBe('default')
  })
})

describe('mountSurface — no mount points', () => {
  it('does nothing when neither #callora-app nor an island is present', () => {
    document.body.innerHTML = '<p>plain</p>'
    const registry = createSurfaceRegistry()
    registry.registerView({ id: 'home', component: probe('home') })

    mountSurface(registry, document)

    expect(document.querySelector('[data-testid="home"]')).toBeNull()
  })
})
