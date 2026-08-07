import { describe, expect, it } from 'vitest'
import { flattenPages, type PageNode } from './page-tree'

function page(surfaceKey: string, parentSurfaceKey: string | null = null, position = 0): PageNode {
  return {
    surfaceKey,
    label: surfaceKey,
    parentSurfaceKey,
    position,
    layoutKey: null,
    hasPublishedVersion: false,
  }
}

const keys = (rows: { page: PageNode }[]) => rows.map((row) => row.page.surfaceKey)

describe('flattenPages', () => {
  it('stellt jede Wurzel vor ihre Nachfahren', () => {
    const rows = flattenPages([
      page('kunden', 'cc', 1),
      page('cc'),
      page('arbeitsplatz', 'cc', 0),
    ])

    expect(keys(rows)).toEqual(['cc', 'arbeitsplatz', 'kunden'])
  })

  it('gibt die Tiefe an, damit die Einrückung stimmt', () => {
    const rows = flattenPages([page('cc'), page('kunden', 'cc'), page('detail', 'kunden')])

    expect(rows.map((row) => row.depth)).toEqual([0, 1, 2])
  })

  it('zeigt einen Knoten mit fehlendem Elternteil als Wurzel', () => {
    // Ein Knoten, den man nicht sieht, ist einer, den man nicht reparieren kann.
    expect(keys(flattenPages([page('waise', 'gibt-es-nicht')]))).toEqual(['waise'])
  })

  it('verliert keinen Knoten aus einem Zyklus', () => {
    const rows = flattenPages([page('a', 'b'), page('b', 'a')])

    expect(keys(rows).sort()).toEqual(['a', 'b'])
  })
})
