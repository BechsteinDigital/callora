import { describe, it, expect } from 'vitest'
import { coerceInputToJsonValue, displayJsonValue } from './themesValues'

describe('coerceInputToJsonValue', () => {
  it('parses valid JSON (number, bool, object)', () => {
    expect(coerceInputToJsonValue('42')).toBe(42)
    expect(coerceInputToJsonValue('true')).toBe(true)
    expect(coerceInputToJsonValue('{"a":1}')).toEqual({ a: 1 })
  })

  it('keeps non-JSON text as a string', () => {
    expect(coerceInputToJsonValue('#ffffff')).toBe('#ffffff')
    expect(coerceInputToJsonValue('  hello ')).toBe('hello')
  })
})

describe('displayJsonValue', () => {
  it('returns an empty string for null/undefined/empty', () => {
    expect(displayJsonValue(null)).toBe('')
    expect(displayJsonValue(undefined)).toBe('')
    expect(displayJsonValue('')).toBe('')
  })

  it('unwraps a JSON string to its inner text', () => {
    expect(displayJsonValue('"#ffffff"')).toBe('#ffffff')
  })

  it('shows raw JSON for non-string values', () => {
    expect(displayJsonValue('42')).toBe('42')
    expect(displayJsonValue('{"a":1}')).toBe('{"a":1}')
  })

  it('round-trips a color string through display then coerce', () => {
    // "#fff" stored as JSON string → shown as #fff → coerced back to "#fff".
    expect(coerceInputToJsonValue(displayJsonValue('"#fff"'))).toBe('#fff')
  })
})
