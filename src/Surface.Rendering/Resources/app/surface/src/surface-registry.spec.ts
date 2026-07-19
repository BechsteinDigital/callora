import { describe, it, expect } from 'vitest'
import { defineComponent } from 'vue'
import { createSurfaceRegistry } from './surface-registry'

const stub = defineComponent({ render: () => null })

describe('surface registry', () => {
  it('starts empty — the grundgerüst ships no views', () => {
    expect(createSurfaceRegistry().views).toHaveLength(0)
  })

  it('registers a view and de-duplicates by id', () => {
    const registry = createSurfaceRegistry()
    registry.registerView({ id: 'a', component: stub })
    registry.registerView({ id: 'a', component: stub })

    expect(registry.views).toHaveLength(1)
  })

  it('orders views by the optional order field', () => {
    const registry = createSurfaceRegistry()
    registry.registerView({ id: 'b', component: stub, order: 20 })
    registry.registerView({ id: 'a', component: stub, order: 10 })

    expect(registry.views.map((view) => view.id)).toEqual(['a', 'b'])
  })
})
