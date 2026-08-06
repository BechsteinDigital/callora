import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * The SDK declares the block contract a second time — it is a standalone package and
 * cannot import from the runtime it describes. That duplication is a real risk: a field
 * added on one side and forgotten on the other produces a plugin that compiles against
 * a promise the runtime does not keep, and nothing says so until it silently does
 * nothing at runtime.
 *
 * So the field names are compared. Not the types — that would need a compiler on both
 * sides — but names catch the failure that actually happens: someone extends one file.
 *
 * If the SDK ever ships from its own repository this test loses its anchor. Then the
 * answer is to generate one side from the other, not to delete the check.
 */
const RUNTIME_CONTRACT = resolve(
  process.cwd(),
  '../../src/Surface.Rendering/Resources/app/surface/src/blocks/block-contract.ts',
)

const SDK_CONTRACT = resolve(process.cwd(), 'src/index.ts')

function fieldsOf(source: string, interfaceName: string): string[] {
  // Generisch oder nicht: BlockControl<T = unknown> und BlockControl sind dieselbe
  // Deklaration, und ein Muster, das nur eine Form findet, würde bei einem hinzugefügten
  // Typparameter fälschlich Drift melden.
  const declaration = new RegExp(`export interface ${interfaceName}(<[^>]*>)? \\{`)
  const start = source.search(declaration)
  if (start < 0) {
    throw new Error(`Interface ${interfaceName} nicht gefunden.`)
  }

  const body = source.slice(start, source.indexOf('\n}', start))
  return [...body.matchAll(/^\s{2}(\w+)\??:/gm)].map((match) => match[1]!).sort()
}

describe('block contract: SDK ↔ runtime', () => {
  const runtime = readFileSync(RUNTIME_CONTRACT, 'utf8')
  const sdk = readFileSync(SDK_CONTRACT, 'utf8')

  it.each(['BlockDefinition', 'BlockControl', 'BlockCategory'])(
    '%s declares the same fields on both sides',
    (name) => {
      expect(fieldsOf(sdk, name)).toEqual(fieldsOf(runtime, name))
    },
  )

  it('the appearance types are the same closed set on both sides', () => {
    // Die geschlossene Menge ist die Guardrail. Liefe sie auseinander, könnte ein
    // Plugin gegen einen Typ bauen, den die Runtime als reserviert ablehnt.
    const extract = (source: string) =>
      source
        .slice(source.indexOf('AppearanceControlType ='))
        .split('\n')[0]!
        .replace('AppearanceControlType =', '')
        .split('|')
        .map((part) => part.trim())
        .filter(Boolean)
        .sort()

    expect(extract(sdk)).toEqual(extract(runtime))
  })
})
