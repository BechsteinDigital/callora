import { describe, expect, it } from 'vitest'
import type { BlockControls } from '@callora/surface'
import { contextKeyOf, displayValue, fieldsFor, valuesOf } from './control-fields'

const ROLES = ['color-bg', 'color-fg', 'font-sans', 'space-4']

describe('fieldsFor', () => {
  it('bildet jeden bekannten Typ auf sein Feld ab', () => {
    const controls: BlockControls = {
      title: { type: 'text', label: 'Titel' },
      max: { type: 'number', label: 'Anzahl' },
      compact: { type: 'toggle', label: 'Kompakt' },
      mode: { type: 'select', label: 'Modus', options: [{ value: 'a', label: 'A' }] },
      call: { type: 'context', label: 'Anruf' },
    }

    const kinds = fieldsFor(controls, {}, ROLES).map((field) => `${field.name}:${field.kind}`)

    expect(kinds).toEqual([
      'title:text',
      'max:number',
      'compact:toggle',
      'mode:choice',
      'call:contextKey',
    ])
  })

  it('benennt einen Typ ohne Feld, statt ihn wegzulassen', () => {
    // Weggelassen sähe er aus wie eine Einstellung, die es nicht gibt — und niemand käme auf
    // die Idee, sie zu vermissen.
    const controls: BlockControls = { bild: { type: 'media', label: 'Bild' } }

    const [field] = fieldsFor(controls, {}, ROLES)

    expect(field.kind).toBe('unsupported')
    expect(field.name).toBe('bild')
  })

  it('bietet einem Erscheinungs-Control nur die Token seiner Achse an', () => {
    const controls: BlockControls = { farbe: { type: 'colorToken', label: 'Farbe' } }

    const [field] = fieldsFor(controls, {}, ROLES)

    expect(field.options.map((option) => option.value)).toEqual(['color-bg', 'color-fg'])
  })

  it('lässt eigene options eines Erscheinungs-Controls NICHT durch', () => {
    // Das ist der Weg, auf dem ein freier Farbwert doch noch hereinkäme — und mit ihm wäre die
    // Zusicherung der Token-Achse in einer Registrierung aufgehoben.
    const controls: BlockControls = {
      farbe: {
        type: 'colorToken',
        label: 'Farbe',
        options: [{ value: '#ff0000', label: 'Knallrot' }],
      },
    }

    const [field] = fieldsFor(controls, {}, ROLES)

    expect(field.options.map((option) => option.value)).not.toContain('#ff0000')
  })

  it('lässt `variant` aus den options des Blocks wählen, nicht aus Token', () => {
    const controls: BlockControls = {
      stil: { type: 'variant', label: 'Stil', options: [{ value: 'ghost', label: 'Ghost' }] },
    }

    const [field] = fieldsFor(controls, {}, ROLES)

    expect(field.options.map((option) => option.value)).toEqual(['ghost'])
  })

  it('wendet visibleWhen an — und ein fehlendes Prädikat heißt immer, nicht nie', () => {
    const controls: BlockControls = {
      mode: { type: 'text', label: 'Modus' },
      detail: {
        type: 'text',
        label: 'Detail',
        visibleWhen: (values) => values.mode === 'advanced',
      },
    }

    expect(fieldsFor(controls, { mode: 'simple' }, ROLES).map((f) => f.name)).toEqual(['mode'])
    expect(fieldsFor(controls, { mode: 'advanced' }, ROLES).map((f) => f.name)).toEqual([
      'mode',
      'detail',
    ])
  })
})

describe('displayValue', () => {
  const control = { type: 'text' as const, label: 'Titel', default: 'Standard' }

  it('zeigt den gebundenen Wert', () => {
    expect(displayValue({ source: 'static', value: 'Gesetzt' }, control)).toBe('Gesetzt')
  })

  it('fällt ohne Bindung auf den Default des Blocks zurück', () => {
    expect(displayValue(undefined, control)).toBe('Standard')
  })

  it('erfindet für eine Kontext-Bindung keinen Wert', () => {
    // Sie hat einen Schlüssel und erst zur Laufzeit einen Wert. Einen zu zeigen hieße, dem
    // Redakteur etwas vorzuführen, das die Fläche so nicht rendert.
    expect(displayValue({ source: 'context', key: 'x/v1' }, control)).toBe('Standard')
  })
})

describe('valuesOf', () => {
  it('gibt visibleWhen Werte, keine Bindungen', () => {
    // Ein Prädikat fragt „ist mode gleich advanced", nicht „ist die Bindung von mode eine
    // statische mit dem Wert advanced".
    const controls: BlockControls = {
      mode: { type: 'text', label: 'Modus', default: 'simple' },
      other: { type: 'text', label: 'Anderes' },
    }

    const values = valuesOf({ mode: { source: 'static', value: 'advanced' } }, controls)

    expect(values).toEqual({ mode: 'advanced', other: undefined })
  })
})

describe('contextKeyOf', () => {
  it('liest den Schlüssel nur aus einer Kontext-Bindung', () => {
    expect(contextKeyOf({ source: 'context', key: 'crm.lead/v1' })).toBe('crm.lead/v1')
    expect(contextKeyOf({ source: 'static', value: 'crm.lead/v1' })).toBe('')
    expect(contextKeyOf(undefined)).toBe('')
  })
})
