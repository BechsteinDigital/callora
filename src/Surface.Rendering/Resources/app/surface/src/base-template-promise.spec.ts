// @vitest-environment node
//
// Liest die Vorlage von der Platte; unter happy-dom löst node:fs hier nicht auf. Dieselbe
// Ausnahme wie in public/exports.test.ts, und aus demselben Grund.
import { readFileSync } from 'node:fs'
import { describe, it, expect } from 'vitest'

/**
 * Die zweite Hälfte des Befunds: Die Vorlage behauptete, ohne die Kontext-Attribute falle die
 * Hydration „still auf Defaults zurück". Das tut sie nicht — sie lädt dann gar nichts. Nach
 * CLAUDE.md gewinnt der Code, und die Aussage war ein Fehler.
 */
describe('die Zusage der Basisvorlage', () => {
  it('verspricht keinen Rückfall auf Defaults, den es nicht gibt', () => {
    const template = readFileSync(
      `${process.cwd()}/../../../Resources/views/surface/base.njk`,
      'utf8',
    )

    expect(template).not.toMatch(/falls?\s+back\s+to\s+defaults/i)
    expect(template).toContain('NOTHING hydrates')
  })
})
