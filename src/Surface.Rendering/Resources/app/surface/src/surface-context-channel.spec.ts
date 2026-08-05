import { describe, it, expect, vi } from 'vitest'
import {
  createSurfaceContextChannel,
  isSurfaceContextKey,
  type SurfaceContextChannel,
} from './surface-context-channel'

const LEAD_SELECTION = 'crm.lead-selection/v1'

function channel(): SurfaceContextChannel {
  return createSurfaceContextChannel('acme', 'portal')
}

describe('context keys', () => {
  it.each(['crm.lead-selection/v1', 'teleclinic.patient/v2', 'a.b.c/v10'])(
    'accepts the namespaced, versioned key %s',
    (key) => {
      expect(isSurfaceContextKey(key)).toBe(true)
    },
  )

  it.each([
    'lead-selection/v1', // not namespaced
    'crm.lead-selection', // not versioned
    'CRM.lead/v1', // not lowercase
    'crm.lead/v', // no version number
    'crm..lead/v1', // empty segment
  ])('refuses %s', (key) => {
    expect(isSurfaceContextKey(key)).toBe(false)
  })
})

describe('publishing and consuming', () => {
  it('delivers a published value to every subscriber', () => {
    const bus = channel()
    const phone = vi.fn()
    const video = vi.fn()
    bus.subscribe(LEAD_SELECTION, phone)
    bus.subscribe(LEAD_SELECTION, video)

    bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' }).publish({ id: 42 })

    expect(phone).toHaveBeenCalledWith({ id: 42 })
    expect(video).toHaveBeenCalledWith({ id: 42 })
    expect(bus.read(LEAD_SELECTION)).toEqual({ id: 42 })
  })

  it('hands a late subscriber the current snapshot immediately', () => {
    const bus = channel()
    bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' }).publish({ id: 7 })

    const late = vi.fn()
    bus.subscribe(LEAD_SELECTION, late)

    // An island that mounts after the publisher must not sit empty until the next change.
    expect(late).toHaveBeenCalledWith({ id: 7 })
  })

  it('stops delivering after unsubscribe', () => {
    const bus = channel()
    const handler = vi.fn()
    const unsubscribe = bus.subscribe(LEAD_SELECTION, handler)
    const publisher = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' })

    unsubscribe()
    publisher.publish({ id: 1 })

    expect(handler).not.toHaveBeenCalled()
    expect(bus.diagnostics()[0].subscriberCount).toBe(0)
  })

  it('keeps telling the other subscribers when one throws', () => {
    const bus = channel()
    const healthy = vi.fn()
    bus.subscribe(LEAD_SELECTION, () => {
      throw new Error('consumer exploded')
    })
    bus.subscribe(LEAD_SELECTION, healthy)
    vi.spyOn(console, 'error').mockImplementation(() => {})

    bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' }).publish({ id: 1 })

    expect(healthy).toHaveBeenCalled()
  })
})

describe('publisher ownership', () => {
  it('refuses a second publisher for a single-cardinality key and records it', () => {
    const bus = channel()
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' })

    const intruder = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'other' })
    intruder.publish({ id: 99 })

    expect(intruder.accepted).toBe(false)
    expect(bus.read(LEAD_SELECTION)).toBeUndefined()
    // Recorded, not swallowed: two plugins claiming one context is a composition
    // mistake somebody has to be able to see.
    expect(bus.diagnostics()[0].rejectedPublishers).toEqual(['other'])
  })

  it('allows several publishers when the key says multiple', () => {
    const bus = channel()
    const first = bus.providePublisher({
      key: LEAD_SELECTION,
      publisherPluginId: 'crm',
      cardinality: 'multiple',
    })
    const second = bus.providePublisher({
      key: LEAD_SELECTION,
      publisherPluginId: 'import',
      cardinality: 'multiple',
    })

    expect(first.accepted && second.accepted).toBe(true)
    expect(bus.diagnostics()[0].publishers).toEqual(['crm', 'import'])
  })

  it('refuses a key that is not namespaced and versioned', () => {
    const bus = channel()
    vi.spyOn(console, 'warn').mockImplementation(() => {})

    const publisher = bus.providePublisher({ key: 'leads', publisherPluginId: 'crm' })
    publisher.publish({ id: 1 })

    expect(publisher.accepted).toBe(false)
    expect(bus.read('leads')).toBeUndefined()
  })

  it('rejects a value the publisher declared invalid', () => {
    const bus = channel()
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    const publisher = bus.providePublisher({
      key: LEAD_SELECTION,
      publisherPluginId: 'crm',
      validate: (value) => typeof value === 'object' && value !== null && 'id' in value,
    })

    publisher.publish('not a lead' as never)

    expect(bus.read(LEAD_SELECTION)).toBeUndefined()
    expect(bus.diagnostics()[0].rejectedValues).toBe(1)
  })
})

describe('lifecycle', () => {
  it('clears the context when its last publisher goes away', () => {
    const bus = channel()
    const handler = vi.fn()
    bus.subscribe(LEAD_SELECTION, handler)
    const publisher = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' })
    publisher.publish({ id: 3 })

    publisher.dispose()

    // Consumers are told the context is gone rather than holding a snapshot nobody
    // maintains any more.
    expect(handler).toHaveBeenLastCalledWith(undefined)
    expect(bus.read(LEAD_SELECTION)).toBeUndefined()
  })

  it('frees the key so another plugin can take it over', () => {
    const bus = channel()
    const first = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' })
    first.dispose()

    const second = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'other' })

    expect(second.accepted).toBe(true)
  })

  it('ignores publishing through a disposed handle', () => {
    const bus = channel()
    const publisher = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' })
    publisher.dispose()

    publisher.publish({ id: 5 })

    expect(bus.read(LEAD_SELECTION)).toBeUndefined()
  })

  it('clearing tells subscribers without releasing the key', () => {
    const bus = channel()
    const handler = vi.fn()
    bus.subscribe(LEAD_SELECTION, handler)
    const publisher = bus.providePublisher({ key: LEAD_SELECTION, publisherPluginId: 'crm' })
    publisher.publish({ id: 8 })

    publisher.clear()

    expect(handler).toHaveBeenLastCalledWith(undefined)
    expect(bus.diagnostics()[0].publishers).toEqual(['crm'])
  })
})

describe('binding', () => {
  it('names the workspace and surface it belongs to', () => {
    const bus = channel()

    expect(bus.workspaceKey).toBe('acme')
    expect(bus.surfaceKey).toBe('portal')
  })
})
