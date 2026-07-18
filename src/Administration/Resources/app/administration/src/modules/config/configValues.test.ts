import { describe, it, expect } from 'vitest'
import { coerceInputToJsonValue, displayJsonValue } from './configValues'

describe('coerceInputToJsonValue', () => {
  it('parses valid JSON primitives', () => {
    expect(coerceInputToJsonValue('42')).toBe(42)
    expect(coerceInputToJsonValue('true')).toBe(true)
    expect(coerceInputToJsonValue('"text"')).toBe('text')
  })

  it('parses JSON objects and arrays', () => {
    expect(coerceInputToJsonValue('{"a":1}')).toEqual({ a: 1 })
    expect(coerceInputToJsonValue('[1,2]')).toEqual([1, 2])
  })

  it('treats non-JSON text as a string', () => {
    expect(coerceInputToJsonValue('hello')).toBe('hello')
  })

  it('trims before parsing', () => {
    expect(coerceInputToJsonValue('  42  ')).toBe(42)
  })
})

describe('displayJsonValue', () => {
  it('renders an em dash for absent or empty values', () => {
    expect(displayJsonValue(undefined)).toBe('—')
    expect(displayJsonValue(null)).toBe('—')
    expect(displayJsonValue('')).toBe('—')
  })

  it('unwraps a JSON string to its inner text', () => {
    expect(displayJsonValue('"hello"')).toBe('hello')
  })

  it('shows the raw JSON for non-string values', () => {
    expect(displayJsonValue('42')).toBe('42')
    expect(displayJsonValue('true')).toBe('true')
  })

  it('falls back to the raw text when not JSON', () => {
    expect(displayJsonValue('not json')).toBe('not json')
  })
})
