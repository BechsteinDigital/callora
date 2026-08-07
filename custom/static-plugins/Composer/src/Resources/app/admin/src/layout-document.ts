import type { Binding } from '@callora/surface'

/**
 * Das Layout-Dokument, wie der Editor es hält, und die Änderungen daran.
 *
 * Die Änderungen sind **reine Funktionen**, die ein neues Dokument zurückgeben, statt das
 * bestehende zu verändern. Das Dokument ist laut §7.2 ein unveränderlicher Schnappschuss je
 * Version; im Entwurf genauso zu arbeiten hat drei greifbare Folgen: Vue sieht eine neue
 * Referenz und rendert verlässlich, ein Vergleich mit dem zuletzt gespeicherten Stand ist ein
 * Referenzvergleich, und Rückgängig ist später das Aufheben der alten Referenz statt einer
 * zweiten Buchführung.
 */

/** Ein Block im Dokument. `config` bildet Control-Namen auf Bindungen ab. */
export interface LayoutBlock {
  blockId: string
  region: string
  position: number
  config?: Record<string, Binding<unknown>>
  visibility?: string
}

export interface LayoutSection {
  layout: string
  position: number
  spacing?: string
  surfaceRole?: string
  visibility?: string
  blocks: LayoutBlock[]
}

export interface LayoutDocument {
  sections: LayoutSection[]
}

/**
 * Wo ein Block steht. Index statt Schlüssel, weil ein Block keinen eigenen hat: `blockId`
 * benennt die Art, nicht das Exemplar, und zwei gleiche Blöcke in einer Sektion sind erlaubt.
 */
export interface BlockAddress {
  sectionIndex: number
  blockIndex: number
}

/** Ein leeres Dokument — das, womit ein Layout anfängt. */
export function emptyDocument(): LayoutDocument {
  return { sections: [] }
}

/**
 * Liest ein Dokument, wie es über die Leitung kam. Total: Was nicht passt, wird zu leer statt
 * zu einem Fehler. Ein Editor, der an einem alten oder halb kaputten Dokument abstürzt, ist
 * genau der Editor, mit dem man es nicht mehr reparieren kann.
 */
export function readDocument(raw: unknown): LayoutDocument {
  const sections = (raw as LayoutDocument | null)?.sections
  if (!Array.isArray(sections)) {
    return emptyDocument()
  }

  return {
    sections: sections
      .filter((section): section is LayoutSection => isObject(section))
      .map((section) => ({
        ...section,
        blocks: Array.isArray(section.blocks) ? section.blocks.filter(isObject) : [],
      })),
  }
}

/**
 * Hängt eine Sektion mit diesem Layout hinten an.
 *
 * `position` wird fortlaufend vergeben statt aus der Länge abgeleitet: Ein Dokument mit Lücken
 * oder doppelten Positionen (etwa aus einem Rückrollen) bekäme sonst eine Sektion, die vor
 * einer bestehenden landet.
 */
export function addSection(document: LayoutDocument, layout: string): LayoutDocument {
  const highest = document.sections.reduce(
    (max, section) => Math.max(max, section.position ?? 0),
    -1,
  )

  return {
    sections: [...document.sections, { layout, position: highest + 1, blocks: [] }],
  }
}

/**
 * Ändert das Layout einer Sektion. Die Blöcke bleiben, wo sie sind — auch die in Regionen, die
 * das neue Layout nicht hat.
 *
 * Sie umzuhängen wäre die scheinbar hilfreiche Variante und die, die Arbeit vernichtet: Wer ein
 * Layout nur ausprobiert und zurückwechselt, fände seine Seitenspalte im Hauptbereich wieder,
 * ohne dass irgendetwas das rückgängig machen könnte. So bleibt der Wechsel umkehrbar, und der
 * Canvas zeigt die heimatlosen Blöcke sichtbar an.
 */
export function setSectionLayout(
  document: LayoutDocument,
  sectionIndex: number,
  layout: string,
): LayoutDocument {
  const section = document.sections[sectionIndex]
  if (!section || section.layout === layout) {
    return document
  }

  const sections = [...document.sections]
  sections[sectionIndex] = { ...section, layout }
  return { sections }
}

/** Der Block an dieser Stelle, oder undefined. */
export function blockAt(
  document: LayoutDocument,
  address: BlockAddress | null,
): LayoutBlock | undefined {
  if (!address) {
    return undefined
  }

  return document.sections[address.sectionIndex]?.blocks[address.blockIndex]
}

/**
 * Setzt die Bindung eines Controls und gibt das neue Dokument zurück.
 *
 * Eine Adresse, die es nicht gibt, lässt das Dokument unverändert — sie entsteht, wenn ein
 * Panel noch offen ist, während das Dokument darunter kürzer geworden ist. Dann nichts zu tun
 * ist richtiger, als an einer anderen Stelle zu schreiben, die zufällig denselben Index hat.
 */
export function setBlockBinding(
  document: LayoutDocument,
  address: BlockAddress,
  control: string,
  binding: Binding<unknown>,
): LayoutDocument {
  return mapBlock(document, address, (block) => ({
    ...block,
    config: { ...block.config, [control]: binding },
  }))
}

/**
 * Nimmt die Bindung eines Controls heraus — der Block fällt für dieses Control auf seinen
 * `default` zurück.
 *
 * Das ist nicht dasselbe wie eine Bindung auf den Default-Wert: Ein entferntes Control folgt
 * dem Block, wenn dessen Autor den Default ändert; ein eingefrorener Wert tut das nicht.
 */
export function clearBlockBinding(
  document: LayoutDocument,
  address: BlockAddress,
  control: string,
): LayoutDocument {
  return mapBlock(document, address, (block) => {
    if (!block.config || !(control in block.config)) {
      return block
    }

    const { [control]: _removed, ...rest } = block.config
    return { ...block, config: rest }
  })
}

function mapBlock(
  document: LayoutDocument,
  address: BlockAddress,
  change: (block: LayoutBlock) => LayoutBlock,
): LayoutDocument {
  const section = document.sections[address.sectionIndex]
  const block = section?.blocks[address.blockIndex]
  if (!section || !block) {
    return document
  }

  const changed = change(block)
  if (changed === block) {
    return document
  }

  const blocks = [...section.blocks]
  blocks[address.blockIndex] = changed
  const sections = [...document.sections]
  sections[address.sectionIndex] = { ...section, blocks }
  return { sections }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
