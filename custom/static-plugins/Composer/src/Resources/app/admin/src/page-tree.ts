/**
 * Der Seitenbaum des Editors — die Flächen des Workspaces mit ihren Layouts.
 *
 * Das Abflachen steht als reine Funktion hier, weil es die Stelle ist, an der ein kaputter Baum
 * gefährlich würde: Ein Zyklus in den Daten darf den Editor nicht zum Hängen bringen. Genau
 * dort, wo man ihn reparieren würde.
 */

export interface PageNode {
  surfaceKey: string
  label: string
  parentSurfaceKey: string | null
  position: number
  /** Das Layout dieser Fläche, oder null — dann ist sie eine Gliederungsebene. */
  layoutKey: string | null
  hasPublishedVersion: boolean
}

export interface PageRow {
  page: PageNode
  depth: number
}

/**
 * Der Baum in Anzeigereihenfolge: jede Wurzel, direkt gefolgt von ihren Nachfahren.
 *
 * Geschwister sortieren nach `position`, bei Gleichstand nach Schlüssel — sonst hinge die
 * Reihenfolge an der der Antwort, und zwei Aufrufe zeigten dieselbe Struktur verschieden.
 *
 * Ein Knoten, dessen Elternteil fehlt, erscheint als Wurzel; Knoten aus einem Zyklus kommen
 * hinten dran. Beides, weil ein Knoten, den man nicht sieht, einer ist, den man nicht
 * reparieren kann.
 */
export function flattenPages(pages: readonly PageNode[]): PageRow[] {
  const known = new Set(pages.map((page) => page.surfaceKey))
  const childrenOf = new Map<string, PageNode[]>()
  const roots: PageNode[] = []

  for (const page of pages) {
    const parentKey = page.parentSurfaceKey
    if (parentKey && known.has(parentKey)) {
      childrenOf.set(parentKey, [...(childrenOf.get(parentKey) ?? []), page])
    } else {
      roots.push(page)
    }
  }

  const byOrder = (a: PageNode, b: PageNode) =>
    (a.position ?? 0) - (b.position ?? 0) || a.surfaceKey.localeCompare(b.surfaceKey)

  const rows: PageRow[] = []
  const visited = new Set<string>()
  const stack = [...roots].sort(byOrder).reverse().map((page) => ({ page, depth: 0 }))

  while (stack.length > 0) {
    const entry = stack.pop()!
    if (visited.has(entry.page.surfaceKey)) {
      continue
    }

    visited.add(entry.page.surfaceKey)
    rows.push(entry)

    const children = [...(childrenOf.get(entry.page.surfaceKey) ?? [])].sort(byOrder)
    for (let index = children.length - 1; index >= 0; index--) {
      stack.push({ page: children[index], depth: entry.depth + 1 })
    }
  }

  for (const page of pages) {
    if (!visited.has(page.surfaceKey)) {
      rows.push({ page, depth: 0 })
    }
  }

  return rows
}
