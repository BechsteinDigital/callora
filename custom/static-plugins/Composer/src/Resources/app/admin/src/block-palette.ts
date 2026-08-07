import type { BlockCategory, BlockDefinition } from '@callora/surface'

/**
 * Was der Editor zum Einsetzen anbietet.
 *
 * Die Palette liest dieselbe Registry, aus der der Canvas seine Komponenten nimmt — es gibt
 * keine zweite Liste dessen, was es gibt. Was sie tut, ist filtern und gruppieren.
 */

export interface PaletteEntry {
  block: BlockDefinition
}

export interface PaletteGroup {
  categoryId: string
  label: string
  blocks: BlockDefinition[]
}

/**
 * Ob dieser Block auf einer Fläche erscheinen darf.
 *
 * `surfaces` ist eine Erlaubnisliste; fehlt sie, ist der Block flächenneutral. Ein Block, der
 * ausdrücklich nur `admin` kann, gehört nicht in eine Flächen-Palette — angeboten würde er dort
 * platziert und erschiene auf der veröffentlichten Seite nie, ohne dass jemand erführe, warum.
 */
export function isOfferedOnSurface(block: BlockDefinition): boolean {
  return !block.surfaces || block.surfaces.length === 0 || block.surfaces.includes('surface')
}

/**
 * Die Blöcke nach Kategorie, in der Reihenfolge der registrierten Kategorien.
 *
 * Eine Kategorie, die niemand registriert hat, verschwindet nicht — ihre Blöcke landen unter
 * ihrem eigenen Schlüssel am Ende. Die Registry lässt einen Block mit unbekannter Kategorie
 * bewusst durch (er soll nicht an der Ladereihenfolge zweier Plugins scheitern), und ihn hier
 * doch noch zu verlieren wäre dieselbe Strafe an anderer Stelle.
 */
export function paletteGroups(
  blocks: readonly BlockDefinition[],
  categories: readonly BlockCategory[],
): PaletteGroup[] {
  const offered = blocks.filter(isOfferedOnSurface)
  const byCategory = new Map<string, BlockDefinition[]>()
  for (const block of offered) {
    byCategory.set(block.category, [...(byCategory.get(block.category) ?? []), block])
  }

  const known = categories
    .filter((category) => byCategory.has(category.id))
    .map((category) => ({
      categoryId: category.id,
      label: category.label,
      blocks: byCategory.get(category.id)!,
    }))

  const knownIds = new Set(categories.map((category) => category.id))
  const unnamed = [...byCategory.entries()]
    .filter(([categoryId]) => !knownIds.has(categoryId))
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([categoryId, categoryBlocks]) => ({
      categoryId,
      label: categoryId,
      blocks: categoryBlocks,
    }))

  return [...known, ...unnamed]
}

/** Was beim Ziehen übertragen wird: ein neuer Block oder ein bereits platzierter. */
export type DragPayload =
  | { kind: 'new'; blockId: string }
  | { kind: 'move'; sectionIndex: number; blockIndex: number }

/** Das MIME-artige Format, unter dem die Nutzlast im DataTransfer liegt. */
export const DRAG_FORMAT = 'application/x-callora-block'

/**
 * Liest die Nutzlast eines Drops, oder null.
 *
 * Alles, was von außen kommt, wird geprüft: Ein Drop kann aus einem anderen Fenster, einem
 * Editor oder einem Dateimanager stammen. Ein Objekt, das nicht passt, ist kein Grund, dem
 * Dokument etwas hinzuzufügen.
 */
export function readDragPayload(raw: string | null | undefined): DragPayload | null {
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as Partial<DragPayload>
    if (parsed?.kind === 'new' && typeof parsed.blockId === 'string' && parsed.blockId !== '') {
      return { kind: 'new', blockId: parsed.blockId }
    }

    if (
      parsed?.kind === 'move' &&
      Number.isInteger(parsed.sectionIndex) &&
      Number.isInteger(parsed.blockIndex)
    ) {
      return {
        kind: 'move',
        sectionIndex: parsed.sectionIndex as number,
        blockIndex: parsed.blockIndex as number,
      }
    }
  } catch {
    // Kein JSON — also nichts von uns.
  }

  return null
}
