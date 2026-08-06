import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * A plugin that uses the primitives inside the shell should need no stylesheet of its own: the
 * shell has already loaded the styles for them.
 *
 * That holds only because Vue derives a component's scoped-style id from its file path, and both
 * builds see the same project root — so the same component gets the same `data-v-*` attribute in
 * either. Nothing enforces that; a changed root, a different plugin order or an inlined component
 * would break it silently, and plugin pages would render unstyled in production while every test
 * still passed.
 *
 * Hence this test. It runs only when both builds exist — CI produces them, a plain test run does
 * not.
 */
const root = process.cwd()
const appAssets = `${root}/../../../wwwroot/admin/assets`
const libDir = `${root}/dist-lib`

function scopeIds(files: string[]): Set<string> {
  const found = new Set<string>()
  for (const file of files) {
    for (const match of readFileSync(file, 'utf8').matchAll(/data-v-([a-f0-9]{8})/g)) {
      found.add(match[1])
    }
  }
  return found
}

function filesIn(dir: string, extensions: string[]): string[] {
  if (!existsSync(dir)) {
    return []
  }
  return readdirSync(dir, { recursive: true, encoding: 'utf8' })
    .map((entry) => `${dir}/${entry}`)
    .filter((path) => extensions.some((ext) => path.endsWith(ext)) && existsSync(path))
}

describe('scoped style ids', () => {
  it('the library build introduces none the application build does not already carry', () => {
    const appFiles = filesIn(appAssets, ['.js', '.css'])
    const libFiles = filesIn(libDir, ['.js', '.css'])

    if (appFiles.length === 0 || libFiles.length === 0) {
      // Neither build present — nothing to compare. CI builds both before running this.
      return
    }

    const app = scopeIds(appFiles)
    const lib = scopeIds(libFiles)

    expect(lib.size, 'der Bibliotheks-Build enthält gar keine gescopten Stile').toBeGreaterThan(0)
    expect(
      [...lib].filter((id) => !app.has(id)),
      'Diese Scope-Ids kommen nur im Bibliotheks-Build vor — eine Plugin-Seite bliebe damit ungestylt',
    ).toEqual([])
  })
})
