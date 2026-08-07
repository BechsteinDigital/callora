import { describe, expect, it } from 'vitest'
import { eligibleParents, flattenSurfaceTree } from './surfaceTree'
import type { WorkspaceSurface } from './workspacesApi'

function surface(
  surfaceKey: string,
  parentSurfaceKey: string | null = null,
  position = 0,
): WorkspaceSurface {
  return {
    id: surfaceKey,
    workspaceKey: 'acme',
    surfaceKey,
    displayName: surfaceKey,
    surfaceType: 'spa',
    publicBaseUrl: null,
    publicHost: null,
    publicPathPrefix: '/',
    accessMode: 'Mixed',
    locale: null,
    templatePluginId: null,
    templateVersion: null,
    themePluginId: null,
    themeVersion: null,
    isActive: true,
    createdAtUtc: '2026-08-07T00:00:00Z',
    updatedAtUtc: '2026-08-07T00:00:00Z',
    parentSurfaceKey,
    position,
    requiredClaims: null,
  }
}

const keys = (rows: { surface: WorkspaceSurface }[]) => rows.map((row) => row.surface.surfaceKey)

describe('flattenSurfaceTree', () => {
  it('stellt jede Wurzel vor ihre Nachfahren', () => {
    const rows = flattenSurfaceTree([
      surface('kontakt', 'portal', 1),
      surface('portal'),
      surface('dialer'),
      surface('partner', 'portal', 0),
    ])

    expect(keys(rows)).toEqual(['dialer', 'portal', 'partner', 'kontakt'])
  })

  it('gibt die Tiefe an, damit die Einrückung stimmt', () => {
    const rows = flattenSurfaceTree([
      surface('portal'),
      surface('partner', 'portal'),
      surface('downloads', 'partner'),
    ])

    expect(rows.map((row) => row.depth)).toEqual([0, 1, 2])
  })

  it('sortiert Geschwister nach position, bei Gleichstand nach Schlüssel', () => {
    // Sonst hinge die Reihenfolge an der Reihenfolge der Antwort, und zwei Aufrufe zeigten
    // dieselbe Struktur verschieden an.
    const rows = flattenSurfaceTree([
      surface('c', 'portal', 0),
      surface('a', 'portal', 5),
      surface('b', 'portal', 0),
      surface('portal'),
    ])

    expect(keys(rows)).toEqual(['portal', 'b', 'c', 'a'])
  })

  it('zeigt einen Knoten mit fehlendem Elternteil als Wurzel', () => {
    // Er verschwindet nicht: Ein Knoten, den man nicht sieht, ist einer, den man nicht
    // reparieren kann — und der wahrscheinlichste Grund für einen fehlenden Elternteil ist
    // genau der Fehler, der behoben werden soll.
    const rows = flattenSurfaceTree([surface('waise', 'gibt-es-nicht')])

    expect(keys(rows)).toEqual(['waise'])
    expect(rows[0].depth).toBe(0)
  })

  it('zeigt auch die Knoten eines Zyklus an', () => {
    // Ein Zyklus taucht im Durchlauf gar nicht auf — jeder Knoten darin hat seinen Elternteil
    // im Zyklus, ist also nie Kind einer Wurzel. Ohne Nachlese fehlten sie schlicht, und ein
    // Knoten, den man nicht sieht, ist einer, den man nicht reparieren kann.
    const rows = flattenSurfaceTree([surface('a', 'b'), surface('b', 'a')])

    expect(keys(rows).sort()).toEqual(['a', 'b'])
  })

  it('verliert keinen Knoten, auch nicht aus einem Zyklus', () => {
    const rows = flattenSurfaceTree([
      surface('portal'),
      surface('kind', 'portal'),
      surface('a', 'b'),
      surface('b', 'a'),
    ])

    expect(keys(rows).sort()).toEqual(['a', 'b', 'kind', 'portal'])
  })

  it('verträgt eine leere Menge', () => {
    expect(flattenSurfaceTree([])).toEqual([])
  })
})

describe('eligibleParents', () => {
  const tree = [
    surface('portal'),
    surface('partner', 'portal'),
    surface('downloads', 'partner'),
    surface('dialer'),
  ]

  it('schließt den Knoten selbst und seine Nachfahren aus', () => {
    // Der Server lehnt einen Zyklus ohnehin ab; ihn gar nicht erst anzubieten ist der
    // Unterschied zwischen einer Fehlermeldung und einer Auswahl, die nur Mögliches enthält.
    const parents = eligibleParents(tree, 'portal').map((s) => s.surfaceKey)

    expect(parents).toEqual(['dialer'])
  })

  it('lässt einen Knoten unter einen anderen Zweig ziehen', () => {
    const parents = eligibleParents(tree, 'downloads').map((s) => s.surfaceKey)

    expect(parents).toEqual(['portal', 'partner', 'dialer'])
  })

  it('bietet beim Anlegen alle an', () => {
    expect(eligibleParents(tree, null)).toHaveLength(4)
  })
})
