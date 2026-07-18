import { describe, it, expect, beforeEach } from 'vitest'
import { defineComponent } from 'vue'
import { registerExtension, getExtensions, resetExtensions } from './registry'

const A = defineComponent({ name: 'A', render: () => null })
const B = defineComponent({ name: 'B', render: () => null })

beforeEach(() => resetExtensions())

describe('extension registry', () => {
  it('returns the components registered for a slot', () => {
    registerExtension('users.detail.fields', A)
    expect(getExtensions('users.detail.fields')).toEqual([A])
  })

  it('orders by ascending order, preserving registration order on ties', () => {
    registerExtension('s', A, 10)
    registerExtension('s', B, 1)
    expect(getExtensions('s')).toEqual([B, A])
  })

  it('isolates slots from one another', () => {
    registerExtension('s1', A)
    expect(getExtensions('s2')).toEqual([])
  })
})
