// @vitest-environment node
//
// Liest beide Preset-Dateien von der Platte, deshalb Node statt happy-dom.
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * Beide Runtimes müssen Vue unter DEMSELBEN Namen bereitstellen, und beide Presets müssen
 * dorthin mappen.
 *
 * Ein Block-Bundle ist gegen genau einen Namen gebaut. Der Canvas des Composers ist der Fall,
 * an dem das zählt: Er läuft in der Admin-Shell und rendert Surface-Blöcke. Liefen die Namen
 * auseinander, gäbe es entweder zwei Vue-Instanzen — womit Reaktivität über die Grenze
 * stillschweigend aufhört, ohne Fehlermeldung — oder Blöcke, die im Editor nicht laufen.
 *
 * Als Quelltextvergleich, weil die beiden Pakete keinen Code teilen: Das Surface-Preset aus dem
 * Admin zu importieren hieße, eine Abhängigkeit zwischen zwei bewusst getrennten Paketen
 * einzuführen, nur um eine Zeichenkette zu vergleichen.
 */
const SHARED_GLOBAL = 'CalloraVue'

const root = process.cwd()
const surfacePreset = resolve(
  root,
  '../../../../Surface.Rendering/Resources/app/surface/src/public/vite-preset.ts',
)
const adminPreset = resolve(root, 'src/public/vite-preset.ts')

function mappedGlobal(path: string): string | undefined {
  const source = readFileSync(path, 'utf8')
  return /globals:\s*\{\s*vue:\s*'([^']+)'/.exec(source)?.[1]
}

describe('the shared Vue global', () => {
  it('is what the admin preset maps vue to', () => {
    expect(mappedGlobal(adminPreset)).toBe(SHARED_GLOBAL)
  })

  it('is what the surface preset maps vue to', () => {
    expect(mappedGlobal(surfacePreset)).toBe(SHARED_GLOBAL)
  })

  it('is not hidden inside a runtime-specific object', () => {
    // CalloraAdmin.vue war der alte Weg. Ein Punkt im Namen heißt: das Global gehört einer
    // Runtime, und ein Bundle dagegen läuft nur dort.
    expect(SHARED_GLOBAL).not.toContain('.')
  })
})
