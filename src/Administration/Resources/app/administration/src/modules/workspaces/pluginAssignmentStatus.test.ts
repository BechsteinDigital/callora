import { describe, expect, it } from 'vitest'
import { describeAssignment } from './pluginAssignmentStatus'
import type { WorkspacePluginAssignment } from './workspacesApi'

function plugin(over: Partial<WorkspacePluginAssignment>): WorkspacePluginAssignment {
  return {
    pluginId: 'communication',
    displayName: 'Communication',
    isGloballyActive: true,
    isEntitled: false,
    isActive: false,
    isAssigned: false,
    ...over,
  }
}

describe('describeAssignment', () => {
  it('reports a fully assigned plugin', () => {
    const status = describeAssignment(plugin({ isEntitled: true, isActive: true, isAssigned: true }))

    expect(status.health).toBe('assigned')
    expect(status.tone).toBe('success')
  })

  it('reports an untouched plugin as simply not assigned', () => {
    expect(describeAssignment(plugin({})).health).toBe('unassigned')
  })

  it('flags an entitlement without activation', () => {
    const status = describeAssignment(plugin({ isEntitled: true, isActive: false }))

    expect(status.health).toBe('partial')
    expect(status.tone).toBe('warning')
    expect(status.detail).toContain('nicht aktiviert')
  })

  it('flags an activation whose entitlement was revoked elsewhere', () => {
    // The case the single isAssigned flag hid: revoking on the Berechtigungen
    // page leaves the plugin activated but unusable.
    const status = describeAssignment(plugin({ isEntitled: false, isActive: true }))

    expect(status.health).toBe('partial')
    expect(status.detail).toContain('Berechtigung fehlt')
  })

  it('puts a globally inactive plugin above everything else', () => {
    const status = describeAssignment(plugin({ isGloballyActive: false }))

    expect(status.health).toBe('blocked')
    expect(status.detail).toContain('global aktiviert')
  })

  it('warns when a globally deactivated plugin is still assigned here', () => {
    const status = describeAssignment(
      plugin({ isGloballyActive: false, isEntitled: true, isActive: true, isAssigned: true }),
    )

    expect(status.health).toBe('blocked')
    expect(status.detail).toContain('läuft aber nicht')
  })

  it('always explains the state it reports', () => {
    const combinations = [true, false].flatMap((globallyActive) =>
      [true, false].flatMap((entitled) =>
        [true, false].map((active) =>
          plugin({ isGloballyActive: globallyActive, isEntitled: entitled, isActive: active }),
        ),
      ),
    )

    for (const candidate of combinations) {
      const status = describeAssignment(candidate)
      expect(status.label.length).toBeGreaterThan(0)
      expect(status.detail.length).toBeGreaterThan(0)
    }
  })
})
