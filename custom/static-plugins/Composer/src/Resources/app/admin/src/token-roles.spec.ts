import { describe, expect, it } from 'vitest'
import { collectTokenRoles, isTokenControl, rolesForControlType } from './token-roles'

describe('collectTokenRoles', () => {
  it('nimmt Deklarationen, nicht Verwendungen', () => {
    // Der Unterschied ist der zwischen „diese Rolle gibt es" und „irgendwer liest sie". Ein
    // Block, der ein Token liest, das niemand setzt, darf es nicht zur Auswahl stellen —
    // sonst wählt jemand eine Rolle, die nirgends einen Wert hat.
    const css = `
      :root { --cal-color-fg: #111; }
      .card { color: var(--cal-color-nicht-gesetzt); }
    `

    expect(collectTokenRoles(css)).toEqual(['color-fg'])
  })

  it('sammelt über mehrere Blöcke hinweg und entdoppelt', () => {
    const css = `
      :root { --cal-color-fg: #111; --cal-space-4: 1rem; }
      .theme { --cal-color-fg: #222; --cal-color-bg: #fff; }
    `

    expect(collectTokenRoles(css)).toEqual(['color-bg', 'color-fg', 'space-4'])
  })

  it('lässt fremde Custom Properties liegen', () => {
    // Die Admin-Shell bringt eigene mit. Sie einem Flächen-Block anzubieten hieße, ihm eine
    // Rolle zu geben, die im Frontend nicht existiert.
    const css = ':root { --admin-color-fg: #111; --cal-color-fg: #222; }'

    expect(collectTokenRoles(css)).toEqual(['color-fg'])
  })

  it('verträgt Leerraum vor dem Doppelpunkt', () => {
    expect(collectTokenRoles(':root { --cal-space-4 : 1rem }')).toEqual(['space-4'])
  })
})

describe('rolesForControlType', () => {
  const roles = ['color-bg', 'color-fg', 'font-sans', 'space-4']

  it('gibt jedem Erscheinungs-Typ seine Rollen', () => {
    expect(rolesForControlType('colorToken', roles)).toEqual(['color-bg', 'color-fg'])
    expect(rolesForControlType('spacingToken', roles)).toEqual(['space-4'])
    expect(rolesForControlType('typeToken', roles)).toEqual(['font-sans'])
  })

  it('gibt einem unbekannten Typ nichts statt alles', () => {
    // Wer einen eigenen Erscheinungs-Typ beiträgt, ohne dass ein Präfix dafür bekannt ist,
    // soll eine leere Auswahl sehen und nachfragen — nicht still die Farbrollen als Abstände
    // angeboten bekommen.
    expect(rolesForControlType('gradientToken', roles)).toEqual([])
  })
})

describe('isTokenControl', () => {
  it('erkennt genau die Erscheinungs-Typen, die aus Token wählen', () => {
    expect(isTokenControl('colorToken')).toBe(true)
    expect(isTokenControl('spacingToken')).toBe(true)
    expect(isTokenControl('typeToken')).toBe(true)
    expect(isTokenControl('text')).toBe(false)
    // `variant` ist eine Erscheinungs-Achse, zieht ihre Werte aber aus den `options` des
    // Blocks, nicht aus den Token des Themes.
    expect(isTokenControl('variant')).toBe(false)
  })
})
