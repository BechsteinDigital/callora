import { describe, it, expect } from 'vitest'
import { readSurfaceContext, resolveSurfaceContext } from './surface-context'

const guest = { state: 'guest', subject: { issuer: 'callora.surface-guest', subjectId: '' } }

describe('readSurfaceContext', () => {
  it('reads workspace/surface from the mount root data attributes', () => {
    const el = document.createElement('div')
    el.dataset.workspace = 'acme'
    el.dataset.surface = 'portal'

    expect(readSurfaceContext(el)).toEqual({
      workspaceKey: 'acme',
      surfaceKey: 'portal',
      caller: guest,
    })
  })

  it('falls back to default when attributes are absent', () => {
    expect(readSurfaceContext(document.createElement('div'))).toEqual({
      workspaceKey: 'default',
      surfaceKey: 'default',
      caller: guest,
    })
  })
})

describe('the caller carried on the mount root', () => {
  it('reads a guest context with its stable subject', () => {
    const el = document.createElement('div')
    el.dataset.callerState = 'guest'
    el.dataset.callerSubject = 'g-123'

    expect(readSurfaceContext(el).caller).toEqual({
      state: 'guest',
      subject: { issuer: 'callora.surface-guest', subjectId: 'g-123' },
    })
  })

  it('reads an authenticated caller with issuer, name and claims', () => {
    const el = document.createElement('div')
    el.dataset.callerState = 'authenticated'
    el.dataset.callerIssuer = 'crm.example'
    el.dataset.callerSubject = 'lead-42'
    el.dataset.callerName = 'Erika Muster'
    el.dataset.callerClaims = JSON.stringify({ 'crm.roles': ['agent', 'supervisor'] })

    expect(readSurfaceContext(el).caller).toEqual({
      state: 'authenticated',
      subject: { issuer: 'crm.example', subjectId: 'lead-42' },
      displayName: 'Erika Muster',
      claims: { 'crm.roles': ['agent', 'supervisor'] },
    })
  })

  it('treats anything but an explicit authenticated state as a guest', () => {
    const el = document.createElement('div')
    el.dataset.callerState = 'Authenticated'
    el.dataset.callerIssuer = 'crm.example'
    el.dataset.callerSubject = 'lead-42'

    expect(readSurfaceContext(el).caller.state).toBe('guest')
  })

  it('does not let a guest inherit an issuer from the markup', () => {
    const el = document.createElement('div')
    el.dataset.callerState = 'guest'
    el.dataset.callerIssuer = 'crm.example'

    expect(readSurfaceContext(el).caller.subject.issuer).toBe('callora.surface-guest')
  })

  it('survives a malformed claim bag', () => {
    const el = document.createElement('div')
    el.dataset.callerState = 'authenticated'
    el.dataset.callerIssuer = 'crm.example'
    el.dataset.callerSubject = 'lead-42'
    el.dataset.callerClaims = '{not json'

    const caller = readSurfaceContext(el).caller
    expect(caller.state).toBe('authenticated')
    expect(caller.state === 'authenticated' && caller.claims).toEqual({})
  })

  it('drops claim values that are not arrays', () => {
    const el = document.createElement('div')
    el.dataset.callerState = 'authenticated'
    el.dataset.callerIssuer = 'crm.example'
    el.dataset.callerSubject = 'lead-42'
    el.dataset.callerClaims = JSON.stringify({ 'crm.roles': 'agent', 'crm.tier': ['gold'] })

    const caller = readSurfaceContext(el).caller
    expect(caller.state === 'authenticated' && caller.claims).toEqual({ 'crm.tier': ['gold'] })
  })
})

describe('resolveSurfaceContext', () => {
  it('inherits the context from the nearest ancestor carrying data-workspace', () => {
    const wrapper = document.createElement('main')
    wrapper.dataset.workspace = 'acme'
    wrapper.dataset.surface = 'portal'
    const island = document.createElement('div')
    wrapper.appendChild(island)

    expect(resolveSurfaceContext(island)).toEqual({
      workspaceKey: 'acme',
      surfaceKey: 'portal',
      caller: guest,
    })
  })

  it('reads the element itself when it carries the context', () => {
    const el = document.createElement('div')
    el.dataset.workspace = 'acme'

    expect(resolveSurfaceContext(el)).toEqual({
      workspaceKey: 'acme',
      surfaceKey: 'default',
      caller: guest,
    })
  })

  it('falls back to default when no ancestor carries data-workspace', () => {
    expect(resolveSurfaceContext(document.createElement('div'))).toEqual({
      workspaceKey: 'default',
      surfaceKey: 'default',
      caller: guest,
    })
  })
})
