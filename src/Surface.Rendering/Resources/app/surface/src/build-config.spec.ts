import { describe, expect, it } from 'vitest'

import { browserProcessDefinitions } from './build-constants'

describe('surface production bundle', () => {
  it('replaces Node process environment checks for direct browser execution', () => {
    expect(browserProcessDefinitions).toEqual({
      'process.env.NODE_ENV': JSON.stringify('production'),
    })
  })
})
