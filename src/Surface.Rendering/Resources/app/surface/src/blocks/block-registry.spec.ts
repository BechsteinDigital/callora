import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import {
  blocksForSurface,
  createBlockRegistry,
  isKnownControlType,
  unsatisfiedRequirements,
} from './block-registry'
import type { BlockDefinition } from './block-contract'
import { createSurfaceRegistry } from '../surface-registry'

const Stub = defineComponent({ name: 'Stub', setup: () => () => h('div') })

function block(id: string, overrides: Partial<BlockDefinition> = {}): BlockDefinition {
  return { id, label: id, category: 'general', component: Stub, ...overrides }
}

describe('block registry', () => {
  it('registers a block under its category', () => {
    const registry = createBlockRegistry()
    registry.registerBlockCategory({ id: 'telephony', label: 'Telefonie', icon: 'phone' })
    registry.registerBlock(block('communication.call-list', { category: 'telephony' }))

    expect(registry.blocks.map((b) => b.id)).toEqual(['communication.call-list'])
    expect(registry.problems).toEqual([])
  })

  it('takes any category string — there is no fixed list', () => {
    // Shopwares geschlossenes XSD-Enum ist der Fehler, den wir nicht wiederholen: ein
    // Plugin, das eine Kategorie erfindet, soll dafür keine Host-Änderung brauchen.
    const registry = createBlockRegistry()
    registry.registerBlockCategory({ id: 'völlig.neue-kategorie', label: 'Neu' })
    registry.registerBlock(block('x', { category: 'völlig.neue-kategorie' }))

    expect(registry.problems).toEqual([])
  })

  it('keeps a block whose category nobody registered, and says so', () => {
    // Ein Plugin, das vor dem Kategorie-Anbieter lädt, darf seinen Block nicht verlieren
    // — sonst hinge die Sichtbarkeit an der Ladereihenfolge.
    const registry = createBlockRegistry()
    registry.registerBlock(block('waise', { category: 'gibt-es-nicht' }))

    expect(registry.blocks).toHaveLength(1)
    expect(registry.problems).toEqual([
      { kind: 'unknown-category', blockId: 'waise', category: 'gibt-es-nicht' },
    ])
  })

  it('ignores a second registration of the same id and records it', () => {
    const registry = createBlockRegistry()
    registry.registerBlock(block('doppelt'))
    registry.registerBlock(block('doppelt', { label: 'Anders' }))

    expect(registry.blocks).toHaveLength(1)
    expect(registry.blocks[0]?.label).toBe('doppelt')
    expect(registry.problems).toContainEqual({ kind: 'duplicate-block', id: 'doppelt' })
  })

  it('sorts by order', () => {
    const registry = createBlockRegistry()
    registry.registerBlock(block('spät', { order: 20 }))
    registry.registerBlock(block('früh', { order: 10 }))

    expect(registry.blocks.map((b) => b.id)).toEqual(['früh', 'spät'])
  })

  describe('contributed control types', () => {
    it('accepts a plugin type', () => {
      const registry = createBlockRegistry()
      registry.registerControlType('communication.phoneNumber')

      expect(registry.controlTypes).toContain('communication.phoneNumber')
      expect(registry.problems).toEqual([])
    })

    it.each(['colorToken', 'spacingToken', 'typeToken', 'variant'])(
      'refuses to let a plugin take over the appearance type %s',
      (reserved) => {
        // Die Gestalt-Typen wählen ausschließlich aus --cal-*. Ein Plugin, das hier
        // einen freien Farbwähler beitragen könnte, hebelte die Guardrails aus.
        const registry = createBlockRegistry()
        registry.registerControlType(reserved)

        expect(registry.controlTypes).not.toContain(reserved)
        expect(registry.problems).toContainEqual({ kind: 'reserved-control-type', type: reserved })
      },
    )
  })

  describe('surfaces', () => {
    it('offers a block without a surface list everywhere', () => {
      const registry = createBlockRegistry()
      registry.registerBlock(block('neutral'))

      expect(blocksForSurface(registry, 'surface').map((b) => b.id)).toEqual(['neutral'])
      expect(blocksForSurface(registry, 'admin').map((b) => b.id)).toEqual(['neutral'])
    })

    it('honours a surface list', () => {
      const registry = createBlockRegistry()
      registry.registerBlock(block('nur-admin', { surfaces: ['admin'] }))

      expect(blocksForSurface(registry, 'surface')).toEqual([])
      expect(blocksForSurface(registry, 'admin').map((b) => b.id)).toEqual(['nur-admin'])
    })
  })

  describe('context requirements', () => {
    it('reports a required key nothing on the page provides', () => {
      const registry = createBlockRegistry()
      registry.registerBlock(block('anrufdetails', { requires: ['communication.active-call/v1'] }))

      expect(unsatisfiedRequirements(registry, ['anrufdetails'])).toEqual([
        { blockId: 'anrufdetails', missing: ['communication.active-call/v1'] },
      ])
    })

    it('is satisfied by another placed block', () => {
      const registry = createBlockRegistry()
      registry.registerBlock(block('telefon', { provides: ['communication.active-call/v1'] }))
      registry.registerBlock(block('details', { requires: ['communication.active-call/v1'] }))

      expect(unsatisfiedRequirements(registry, ['telefon', 'details'])).toEqual([])
    })

    it('is not satisfied by a block that is registered but not placed', () => {
      // Der Provider muss AUF DER SEITE liegen; registriert zu sein genügt nicht.
      const registry = createBlockRegistry()
      registry.registerBlock(block('telefon', { provides: ['communication.active-call/v1'] }))
      registry.registerBlock(block('details', { requires: ['communication.active-call/v1'] }))

      expect(unsatisfiedRequirements(registry, ['details'])).toEqual([
        { blockId: 'details', missing: ['communication.active-call/v1'] },
      ])
    })
  })

  it('knows which control types are the host’s', () => {
    expect(isKnownControlType('richText')).toBe(true)
    expect(isKnownControlType('slot')).toBe(true)
    expect(isKnownControlType('communication.phoneNumber')).toBe(false)
  })
})

describe('block ↔ view', () => {
  it('makes a registered block renderable under the same id', () => {
    // Die Block-Id IST die View-Id IST das Insel-Attribut. Eine Identität, damit ein im
    // Editor platzierter Block und eine serverseitig gerenderte Insel dasselbe sind.
    const surface = createSurfaceRegistry('w', 's')
    surface.blocks.registerBlockCategory({ id: 'general', label: 'Allgemein' })
    surface.blocks.registerBlock(block('crm.lead-list'))

    expect(surface.views.map((v) => v.id)).toEqual(['crm.lead-list'])
  })

  it('does not register a view twice when a block id collides with one', () => {
    const surface = createSurfaceRegistry('w', 's')
    surface.registerView({ id: 'kollision', component: Stub })
    surface.blocks.registerBlock(block('kollision'))

    expect(surface.views).toHaveLength(1)
  })
})
