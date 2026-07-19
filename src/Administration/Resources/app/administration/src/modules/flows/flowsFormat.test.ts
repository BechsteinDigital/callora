import { describe, it, expect } from 'vitest'
import { parseJsonField, prettyJson } from './flowsFormat'

describe('parseJsonField', () => {
  it('returns the empty fallback for a blank value', () => {
    expect(parseJsonField('   ', null, 'Bedingungen')).toBeNull()
    expect(parseJsonField('', [], 'Aktionen')).toEqual([])
  })

  it('parses valid JSON', () => {
    expect(parseJsonField('{"a":1}', null, 'Bedingungen')).toEqual({ a: 1 })
    expect(parseJsonField('[{"type":"x"}]', [], 'Aktionen')).toEqual([{ type: 'x' }])
  })

  it('throws a labelled error on invalid JSON', () => {
    expect(() => parseJsonField('not json', null, 'Bedingungen')).toThrow('Bedingungen enthält kein gültiges JSON.')
  })
})

describe('prettyJson', () => {
  it('returns an empty string for null or blank', () => {
    expect(prettyJson(null)).toBe('')
    expect(prettyJson('')).toBe('')
  })

  it('pretty-prints valid JSON', () => {
    expect(prettyJson('{"a":1}')).toBe('{\n  "a": 1\n}')
  })

  it('falls back to the raw value when it is not valid JSON', () => {
    expect(prettyJson('{oops')).toBe('{oops')
  })
})
