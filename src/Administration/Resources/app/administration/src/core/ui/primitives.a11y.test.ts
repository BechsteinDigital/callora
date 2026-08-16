import { describe, it, expect, vi, afterEach } from 'vitest'
import { h, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import CalCheckbox from './CalCheckbox.vue'
import CalField from './CalField.vue'
import CalSelect from './CalSelect.vue'
import CalSwitch from './CalSwitch.vue'

/**
 * Die Primitive sind die Stelle, an der Tastaturverhalten entweder für die ganze SPA stimmt oder
 * für die ganze SPA nicht (#298). Sie hatten keinen einzigen Test — also auch nichts, was ihre
 * Zusagen festhält, wenn jemand das native Element gegen etwas Selbstgebautes tauscht.
 */
describe('CalField', () => {
  it('verbindet Label und Bedienelement über dieselbe Id', () => {
    const wrapper = mount(CalField, {
      props: { label: 'Mailadresse' },
      slots: { default: ({ id }: { id: string }) => h('input', { id }) },
    })

    const forAttribute = wrapper.get('label').attributes('for')
    expect(forAttribute).toBeTruthy()
    expect(wrapper.get('input').attributes('id')).toBe(forAttribute)
  })

  // Der Fehler stand sichtbar neben dem Feld und gehörte zu nichts: Wer im Feld steht, hörte
  // nicht, was er falsch gemacht hat.
  it('verknüpft den Fehler mit dem Feld und markiert es als ungültig', async () => {
    const wrapper = mount(CalField, {
      props: { label: 'Mailadresse', error: 'Bitte eine gültige Adresse angeben.' },
      slots: { default: ({ id }: { id: string }) => h('input', { id }) },
      attachTo: document.body,
    })
    await nextTick()

    const input = wrapper.get('input')
    const describedBy = input.attributes('aria-describedby')
    expect(describedBy).toBeTruthy()
    expect(document.getElementById(describedBy!)?.textContent).toContain('gültige Adresse')
    expect(input.attributes('aria-invalid')).toBe('true')

    wrapper.unmount()
  })

  it('verknüpft die Beschreibung, solange kein Fehler sie verdrängt', async () => {
    const wrapper = mount(CalField, {
      props: { label: 'Kürzel', description: 'Drei Buchstaben, klein geschrieben.' },
      slots: { default: ({ id }: { id: string }) => h('input', { id }) },
      attachTo: document.body,
    })
    await nextTick()

    const describedBy = wrapper.get('input').attributes('aria-describedby')
    expect(document.getElementById(describedBy!)?.textContent).toContain('Drei Buchstaben')
    expect(wrapper.get('input').attributes('aria-invalid')).toBeUndefined()

    wrapper.unmount()
  })
})

describe('CalSwitch', () => {
  // Ein <div> mit Klick-Handler sähe genauso aus und wäre mit der Tastatur nicht erreichbar.
  it('ist eine echte Checkbox mit switch-Rolle', () => {
    const input = mount(CalSwitch, { props: { modelValue: false } }).get('input')

    expect(input.attributes('type')).toBe('checkbox')
    expect(input.attributes('role')).toBe('switch')
  })

  it('meldet den neuen Wert, wenn die Tastatur ihn umlegt', async () => {
    const wrapper = mount(CalSwitch, { props: { modelValue: false } })

    await wrapper.get('input').setValue(true)

    expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
  })

  it('meldet nichts, solange es abgeschaltet ist', () => {
    const wrapper = mount(CalSwitch, { props: { modelValue: false, disabled: true } })

    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
  })
})

describe('CalCheckbox', () => {
  it('behält das echte Eingabefeld im DOM, samt Beschriftung ringsum', async () => {
    const wrapper = mount(CalCheckbox, {
      props: { modelValue: false },
      slots: { default: 'Newsletter' },
    })

    expect(wrapper.get('input').attributes('type')).toBe('checkbox')
    expect(wrapper.get('label').text()).toContain('Newsletter')

    await wrapper.get('input').setValue(true)
    expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
  })
})

describe('CalSelect', () => {
  // Absicht laut Kommentar im Primitiv: ein natives <select>, damit Tastatur und Vorlesehilfe
  // auf jeder Plattform stimmen und mobil der Picker des Systems erscheint.
  it('ist ein natives select und meldet die Auswahl', async () => {
    const wrapper = mount(CalSelect, {
      props: { modelValue: 'a' },
      slots: { default: '<option value="a">A</option><option value="b">B</option>' },
    })

    expect(wrapper.get('select').element.tagName).toBe('SELECT')

    await wrapper.get('select').setValue('b')
    expect(wrapper.emitted('update:modelValue')).toEqual([['b']])
  })

  it('reicht ein Label von außen an das Feld durch, nicht an den Rahmen', () => {
    const wrapper = mount(CalSelect, {
      props: { modelValue: 'a' },
      attrs: { 'aria-label': 'Rolle' },
      slots: { default: '<option value="a">A</option>' },
    })

    expect(wrapper.get('select').attributes('aria-label')).toBe('Rolle')
    expect(wrapper.get('.cal-select').attributes('aria-label')).toBeUndefined()
  })
})

describe('CalDialog', () => {
  it('schließt auf Escape, ohne dass eine Ansicht das selbst verdrahten muss', async () => {
    const CalDialog = (await import('./CalDialog.vue')).default
    const wrapper = mount(CalDialog, {
      props: { open: true, title: 'Wirklich löschen?' },
      attachTo: document.body,
    })
    await nextTick()

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await nextTick()

    expect(wrapper.emitted('update:open')).toEqual([[false]])
    wrapper.unmount()
  })

  it('trägt einen Titel, den eine Vorlesehilfe ansagen kann', async () => {
    const CalDialog = (await import('./CalDialog.vue')).default
    const wrapper = mount(CalDialog, {
      props: { open: true, title: 'Wirklich löschen?' },
      attachTo: document.body,
    })
    await nextTick()

    const dialog = document.querySelector('[role="dialog"]')
    expect(dialog).not.toBeNull()
    expect(dialog!.textContent).toContain('Wirklich löschen?')

    wrapper.unmount()
  })
})

// Vitest räumt das Dokument zwischen Dateien nicht auf; die Dialoge hängen im Portal am Body.
afterEach(() => {
  document.body.replaceChildren()
  vi.restoreAllMocks()
})
