import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { describe, it, expect } from 'vitest'

/**
 * Das Gate aus ADR-024 §5: Die Zahl der fest eingetragenen Oberflächentexte darf nur sinken.
 *
 * Die Migration der Ansichten läuft schrittweise — `t('admin.user.create', 'Benutzer anlegen')`
 * gibt den mitgegebenen Text zurück, solange kein Snippet existiert, und damit ist jede Datei
 * einzeln umstellbar. Ohne eine Zahl daneben wäre „schrittweise" aber dasselbe wie „irgendwann":
 * Jede neue Ansicht brächte neue feste Texte mit, und der Rückstand wüchse schneller, als jemand
 * ihn abbaut.
 *
 * Dieselbe Mechanik wie die Baselines in `ArchitectureRulesTests` und wie das Bundle-Budget: Ein
 * Wert, der nicht mehr stimmt, lässt den Test ebenfalls scheitern — sonst bleibt er stehen, wenn
 * Platz frei geworden ist, und misst irgendwann nichts mehr.
 */
const BASELINE = readBaseline()

describe('fest eingetragene Oberflächentexte', () => {
  it('werden weniger, nie mehr', () => {
    const actual = countHardcodedTexts()

    expect(
      actual,
      actual > BASELINE
        ? `Neue feste Texte (${actual} statt ${BASELINE}). Neue Beschriftungen gehören durch ` +
          `t('schlüssel', 'Text') — der zweite Parameter hält die Ansicht lesbar, bis ein ` +
          `Snippet existiert.`
        : `Es sind nur noch ${actual} statt ${BASELINE}. Bitte die Zahl in hardcoded-texts.json ` +
          `nachziehen, sonst misst das Gate bald nichts mehr.`,
    ).toBe(BASELINE)
  })
})

function readBaseline(): number {
  const file = JSON.parse(
    readFileSync(`${process.cwd()}/src/core/i18n/hardcoded-texts.json`, 'utf8'),
  ) as { count: number }
  return file.count
}

/**
 * Gezählt wird, was ein Benutzer liest: Text zwischen den Tags und die Attribute, die eine
 * Beschriftung tragen. Bewusst eine Heuristik und kein Parser — sie muss nicht exakt sein,
 * sondern vergleichbar über die Zeit.
 */
export function countHardcodedTexts(): number {
  let count = 0
  for (const file of vueFiles(`${process.cwd()}/src`)) {
    const template = file.slice(0, file.indexOf('<script'))
    count += (template.match(/>\s*[^<>{}\s][^<>{}]*[^<>{}\s]\s*</g) ?? []).filter(hasLetters).length
    count += (
      template.match(/\b(?:label|title|placeholder|description|empty-title|empty-description)="[^"{}]+"/g) ?? []
    ).filter(hasLetters).length
  }

  return count
}

function hasLetters(value: string): boolean {
  return /[A-Za-zÄÖÜäöüß]{2,}/.test(value)
}

function vueFiles(directory: string): string[] {
  const files: string[] = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) {
      files.push(...vueFiles(path))
    } else if (entry.name.endsWith('.vue')) {
      files.push(readFileSync(path, 'utf8'))
    }
  }

  return files
}
