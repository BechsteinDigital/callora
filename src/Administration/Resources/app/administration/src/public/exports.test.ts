import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * Guards the package manifest against the one mistake a consumer discovers and we do not: an
 * export that points at a file the build never produces. `npm install` succeeds, the import fails.
 *
 * Runs only when dist-lib exists — the plain test run does not build the library, but CI does,
 * and there the check bites.
 */
const root = process.cwd()
const pkg = JSON.parse(readFileSync(`${root}/package.json`, 'utf8')) as {
  exports: Record<string, string | { types: string; import: string }>
  files: string[]
}

const targets = Object.entries(pkg.exports).flatMap(([subpath, value]) =>
  typeof value === 'string'
    ? [{ subpath, kind: 'file', path: value }]
    : [
        { subpath, kind: 'types', path: value.types },
        { subpath, kind: 'import', path: value.import },
      ],
)

const libraryBuilt = existsSync(`${root}/dist-lib`)

describe('package exports', () => {
  it('declares an entry point for every public barrel', () => {
    const subpaths = Object.keys(pkg.exports)

    expect(subpaths).toContain('.')
    expect(subpaths).toContain('./extensions')
    expect(subpaths).toContain('./components')
    expect(subpaths).toContain('./tokens')
    expect(subpaths).toContain('./patterns')
    expect(subpaths).toContain('./vite-preset')
  })

  it('ships every exported path in the files field, or npm would publish an empty package', () => {
    const shipped = (path: string): boolean => {
      const relative = path.replace(/^\.\//, '')
      return pkg.files.some((entry) => relative === entry || relative.startsWith(`${entry}/`))
    }

    const unshipped = targets.filter((target) => !shipped(target.path))

    expect(unshipped.map((t) => `${t.subpath} → ${t.path}`)).toEqual([])
  })

  it('points at files that exist once the library is built', () => {
    if (!libraryBuilt) {
      // Nothing to assert before `npm run build:lib`; the SCSS export is checked below regardless.
      expect(existsSync(`${root}/${pkg.exports['./tokens.scss'] as string}`)).toBe(true)
      return
    }

    const missing = targets.filter((target) => !existsSync(`${root}/${target.path}`))

    expect(missing.map((m) => `${m.subpath} (${m.kind}) → ${m.path}`)).toEqual([])
  })
})
