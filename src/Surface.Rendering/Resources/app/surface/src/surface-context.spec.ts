import { describe, it, expect } from 'vitest'
import { readSurfaceContext } from './surface-context'

describe('readSurfaceContext', () => {
  it('reads workspace/surface from the mount root data attributes', () => {
    const el = document.createElement('div')
    el.dataset.workspace = 'acme'
    el.dataset.surface = 'portal'

    expect(readSurfaceContext(el)).toEqual({ workspaceKey: 'acme', surfaceKey: 'portal' })
  })

  it('falls back to default when attributes are absent', () => {
    expect(readSurfaceContext(document.createElement('div'))).toEqual({
      workspaceKey: 'default',
      surfaceKey: 'default',
    })
  })
})
