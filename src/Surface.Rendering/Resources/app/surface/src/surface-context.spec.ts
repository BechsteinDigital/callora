import { describe, it, expect } from 'vitest'
import { readSurfaceContext, resolveSurfaceContext } from './surface-context'

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

describe('resolveSurfaceContext', () => {
  it('inherits the context from the nearest ancestor carrying data-workspace', () => {
    const wrapper = document.createElement('main')
    wrapper.dataset.workspace = 'acme'
    wrapper.dataset.surface = 'portal'
    const island = document.createElement('div')
    wrapper.appendChild(island)

    expect(resolveSurfaceContext(island)).toEqual({ workspaceKey: 'acme', surfaceKey: 'portal' })
  })

  it('reads the element itself when it carries the context', () => {
    const el = document.createElement('div')
    el.dataset.workspace = 'acme'

    expect(resolveSurfaceContext(el)).toEqual({ workspaceKey: 'acme', surfaceKey: 'default' })
  })

  it('falls back to default when no ancestor carries data-workspace', () => {
    expect(resolveSurfaceContext(document.createElement('div'))).toEqual({
      workspaceKey: 'default',
      surfaceKey: 'default',
    })
  })
})
