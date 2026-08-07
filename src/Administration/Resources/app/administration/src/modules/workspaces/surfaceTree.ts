import type { WorkspaceSurface } from './workspacesApi'

/**
 * Wie der Surface-Baum in einer Liste erscheint (ADR-019).
 *
 * Die API liefert eine flache Menge mit Elternverweisen; angezeigt wird sie als Baum. Das
 * Abflachen steht hier als reine Funktion, weil es die Stelle ist, an der ein kaputter Baum
 * gefährlich würde: Ein Zyklus in den Daten — aus einer Migration, einem direkten
 * Datenbankeingriff — darf die Verwaltung nicht zum Hängen bringen. Genau dort, wo man ihn
 * reparieren würde.
 */

export interface SurfaceTreeRow {
  surface: WorkspaceSurface
  /** 0 für eine Anwendungswurzel, 1 für ihre Kinder, und so weiter. */
  depth: number
}

/**
 * Der Baum in Anzeigereihenfolge: jede Wurzel, direkt gefolgt von ihren Nachfahren.
 *
 * Geschwister sortieren nach `position`, bei Gleichstand nach Schlüssel — sonst hinge die
 * Reihenfolge an der Reihenfolge der Antwort, und zwei Aufrufe zeigten dieselbe Struktur
 * verschieden an.
 *
 * **Ein Knoten, dessen Elternteil fehlt, erscheint als Wurzel.** Er verschwindet nicht: Ein
 * Knoten, den man nicht sieht, ist ein Knoten, den man nicht reparieren kann — und der
 * wahrscheinlichste Grund für einen fehlenden Elternteil ist genau der Fehler, der behoben
 * werden soll.
 */
export function flattenSurfaceTree(surfaces: readonly WorkspaceSurface[]): SurfaceTreeRow[] {
  const known = new Set(surfaces.map((surface) => surface.surfaceKey))
  const childrenOf = new Map<string, WorkspaceSurface[]>()
  const roots: WorkspaceSurface[] = []

  for (const surface of surfaces) {
    const parentKey = surface.parentSurfaceKey
    if (parentKey && known.has(parentKey)) {
      childrenOf.set(parentKey, [...(childrenOf.get(parentKey) ?? []), surface])
    } else {
      roots.push(surface)
    }
  }

  const bySortOrder = (a: WorkspaceSurface, b: WorkspaceSurface) =>
    (a.position ?? 0) - (b.position ?? 0) || a.surfaceKey.localeCompare(b.surfaceKey)

  const rows: SurfaceTreeRow[] = []
  // Das Besuchsprotokoll ist die günstige Sicherung; die tragende ist die Nachlese unten. Ein
  // Zyklus taucht im Durchlauf nämlich gar nicht auf: Jeder Knoten darin hat seinen Elternteil
  // im Zyklus, ist also nie Kind einer Wurzel — er würde schlicht FEHLEN. Und ein Knoten, den
  // man nicht sieht, ist einer, den man nicht reparieren kann.
  const visited = new Set<string>()
  const stack = [...roots].sort(bySortOrder).reverse().map((surface) => ({ surface, depth: 0 }))

  while (stack.length > 0) {
    const entry = stack.pop()!
    if (visited.has(entry.surface.surfaceKey)) {
      continue
    }

    visited.add(entry.surface.surfaceKey)
    rows.push(entry)

    const children = [...(childrenOf.get(entry.surface.surfaceKey) ?? [])].sort(bySortOrder)
    for (let index = children.length - 1; index >= 0; index--) {
      stack.push({ surface: children[index], depth: entry.depth + 1 })
    }
  }

  // Was der Durchlauf nicht erreicht hat — ein Zyklus, eine Kette ins Leere —, kommt hinten
  // dran. Nicht als Notbehelf, sondern weil genau das die Zeilen sind, wegen derer jemand
  // diese Ansicht öffnet.
  for (const surface of surfaces) {
    if (!visited.has(surface.surfaceKey)) {
      rows.push({ surface, depth: 0 })
    }
  }

  return rows
}

/**
 * Welche Knoten als Elternteil in Frage kommen.
 *
 * Nicht der Knoten selbst und keiner seiner Nachfahren — das wäre ein Zyklus. Der Server lehnt
 * ihn ohnehin ab; ihn hier gar nicht erst anzubieten ist der Unterschied zwischen einer
 * Fehlermeldung und einer Auswahl, die nur Mögliches enthält.
 */
export function eligibleParents(
  surfaces: readonly WorkspaceSurface[],
  ofSurfaceKey: string | null,
): WorkspaceSurface[] {
  if (!ofSurfaceKey) {
    return [...surfaces]
  }

  const rows = flattenSurfaceTree(surfaces)
  const start = rows.findIndex((row) => row.surface.surfaceKey === ofSurfaceKey)
  if (start < 0) {
    return [...surfaces]
  }

  // Die Nachfahren stehen unmittelbar hinter dem Knoten, solange sie tiefer liegen — das ist
  // die Eigenschaft, die das Abflachen mitbringt.
  const excluded = new Set([ofSurfaceKey])
  for (let index = start + 1; index < rows.length && rows[index].depth > rows[start].depth; index++) {
    excluded.add(rows[index].surface.surfaceKey)
  }

  return surfaces.filter((surface) => !excluded.has(surface.surfaceKey))
}

/**
 * Die Claims, die für diesen Knoten gelten, ohne die er selbst verlangt — also das, was von
 * seinen Vorfahren dazukommt.
 *
 * Getrennt vom eigenen Wert, weil die Verwaltung beides zeigen muss und nur das eigene
 * bearbeiten darf: Stünde die ganze Kette im Eingabefeld, schriebe ein Speichern die
 * Anforderung des Elternteils hier fest — und ein späteres Lockern dort bliebe wirkungslos.
 */
export function inheritedClaims(
  surfaces: readonly WorkspaceSurface[],
  surfaceKey: string,
): string[] {
  const byKey = new Map(surfaces.map((surface) => [surface.surfaceKey, surface]))
  const claims = new Set<string>()
  const seen = new Set<string>([surfaceKey])

  let parentKey = byKey.get(surfaceKey)?.parentSurfaceKey ?? null
  while (parentKey && !seen.has(parentKey)) {
    seen.add(parentKey)
    const parent = byKey.get(parentKey)
    if (!parent) {
      break
    }

    for (const claim of parseClaims(parent.requiredClaims)) {
      claims.add(claim)
    }

    parentKey = parent.parentSurfaceKey
  }

  return [...claims].sort()
}

/** Dieselbe Zerlegung wie serverseitig: kommagetrennt, ohne Leerraum, ohne Dubletten. */
export function parseClaims(requiredClaims: string | null | undefined): string[] {
  if (!requiredClaims) {
    return []
  }

  return [...new Set(
    requiredClaims
      .split(',')
      .map((claim) => claim.trim())
      .filter((claim) => claim.length > 0),
  )]
}
