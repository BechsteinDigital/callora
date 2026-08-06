import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { CAL_TOKENS, readToken } from './tokens'

// Resolved from the project root rather than from import.meta.url: under happy-dom the module
// url is not a file: url, so readFileSync cannot take it. Vitest runs with the project root as
// the working directory.
const stylesheet = readFileSync(`${process.cwd()}/src/core/design/tokens.scss`, 'utf8')
const declared = new Set(
  [...stylesheet.matchAll(/^\s+(--cal-[a-z0-9-]+)\s*:/gm)].map((match) => match[1]),
)
const exported = new Set<string>(Object.values(CAL_TOKENS))

describe('token names', () => {
  it('names every token with the --cal- prefix, because the names are public contract', () => {
    for (const name of exported) {
      expect(name.startsWith('--cal-')).toBe(true)
    }
  })

  it('exports every token the stylesheet declares', () => {
    const missing = [...declared].filter((name) => !exported.has(name)).sort()

    expect(missing, 'in tokens.scss deklariert, aber nicht in CAL_TOKENS').toEqual([])
  })

  it('declares every token it exports', () => {
    const orphans = [...exported].filter((name) => !declared.has(name)).sort()

    expect(orphans, 'in CAL_TOKENS, aber in tokens.scss nicht deklariert').toEqual([])
  })

  it('has no duplicate values, so two constants cannot mean the same property', () => {
    expect(exported.size).toBe(Object.keys(CAL_TOKENS).length)
  })
})

describe('readToken', () => {
  it('reads a token value off an element, so a block can branch on the active theme', () => {
    const el = document.createElement('div')
    el.style.setProperty(CAL_TOKENS.accent, '#e4002b')
    document.body.appendChild(el)

    expect(readToken(CAL_TOKENS.accent, el)).toBe('#e4002b')
  })

  it('trims the value, because custom properties keep their leading whitespace', () => {
    const el = document.createElement('div')
    el.style.setProperty(CAL_TOKENS.accent, '  #fff  ')
    document.body.appendChild(el)

    expect(readToken(CAL_TOKENS.accent, el)).toBe('#fff')
  })

  it('returns an empty string for an unset token rather than throwing', () => {
    expect(readToken('--cal-does-not-exist', document.createElement('div'))).toBe('')
  })
})
