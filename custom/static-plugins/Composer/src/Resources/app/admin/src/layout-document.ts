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

/**
 * Wohin ein Block gezogen wird: in welche Sektion, in welche Region, an welche Stelle.
 *
 * `index` zählt die Blöcke DIESER Region in Anzeigereihenfolge — 0 heißt ganz oben, `length`
 * ganz unten. Nicht der Index im `blocks`-Array der Sektion: Der zählt regionenübergreifend
 * und in Speicherreihenfolge, und beides ist nicht das, was jemand beim Ziehen sieht.
 */
export interface DropTarget {
  sectionIndex: number
  region: string
  index: number
}

/**
 * Setzt einen neuen Block an die Zielstelle.
 *
 * Die Positionen der Zielregion werden danach neu durchnummeriert. Das ändert mehr Zahlen als
 * nötig und ist trotzdem richtig: `position` ist eine Reihenfolge, keine Identität, und ein
 * Dokument mit Lücken oder doppelten Positionen — aus einem Rückrollen, einem Layout-Wechsel,
 * einem fremden Editor — würde sonst bei jedem Einfügen ein Stück unberechenbarer.
 */
export function insertBlock(
  document: LayoutDocument,
  target: DropTarget,
  blockId: string,
): LayoutDocument {
  return withRegion(document, target.sectionIndex, target.region, (inRegion) => {
    const placed: LayoutBlock = { blockId, region: target.region, position: 0 }
    return [...inRegion.slice(0, target.index), placed, ...inRegion.slice(target.index)]
  })
}

/**
 * Verschiebt einen Block an die Zielstelle — innerhalb seiner Region oder in eine andere.
 *
 * Der Block behält seine `config`. Er wird verschoben, nicht neu erzeugt: Alles, was jemand an
 * ihm eingestellt hat, zieht mit, auch über die Regionsgrenze.
 */
export function moveBlock(
  document: LayoutDocument,
  from: BlockAddress,
  target: DropTarget,
): LayoutDocument {
  const moving = blockAt(document, from)
  if (!moving || from.sectionIndex !== target.sectionIndex) {
    // Zwischen Sektionen zu ziehen ist noch nicht vorgesehen. Stillschweigend in die falsche
    // Sektion zu schreiben wäre schlimmer als nichts zu tun.
    return document
  }

  const sameRegion = moving.region === target.region

  // Zuerst herausnehmen, dann einsetzen — sonst zählt die Zielstelle den Block noch mit, und
  // "eins weiter unten" landet an derselben Stelle.
  const removed = withRegion(document, from.sectionIndex, moving.region, (inRegion) =>
    inRegion.filter((block) => block !== moving),
  )

  const insertAt = sameRegion && indexInRegion(document, from) < target.index
    ? target.index - 1
    : target.index

  return withRegion(removed, target.sectionIndex, target.region, (inRegion) => {
    const placed: LayoutBlock = { ...moving, region: target.region, position: 0 }
    return [...inRegion.slice(0, insertAt), placed, ...inRegion.slice(insertAt)]
  })
}

/** Nimmt einen Block aus dem Dokument. */
export function removeBlock(document: LayoutDocument, address: BlockAddress): LayoutDocument {
  const block = blockAt(document, address)
  if (!block) {
    return document
  }

  return withRegion(document, address.sectionIndex, block.region, (inRegion) =>
    inRegion.filter((existing) => existing !== block),
  )
}

/** Wo ein Block innerhalb seiner Region steht — die Zählung, die beim Ziehen sichtbar ist. */
export function indexInRegion(document: LayoutDocument, address: BlockAddress): number {
  const block = blockAt(document, address)
  const section = document.sections[address.sectionIndex]
  if (!block || !section) {
    return -1
  }

  return sortedRegion(section, block.region).indexOf(block)
}

/**
 * Wendet eine Änderung auf die Blöcke einer Region an und schreibt das Ergebnis mit
 * fortlaufenden Positionen zurück. Die Blöcke der anderen Regionen bleiben unberührt.
 */
function withRegion(
  document: LayoutDocument,
  sectionIndex: number,
  region: string,
  change: (inRegion: LayoutBlock[]) => LayoutBlock[],
): LayoutDocument {
  const section = document.sections[sectionIndex]
  if (!section) {
    return document
  }

  const others = section.blocks.filter((block) => block.region !== region)
  const changed = change(sortedRegion(section, region)).map((block, index) => ({
    ...block,
    region,
    position: index,
  }))

  const sections = [...document.sections]
  sections[sectionIndex] = { ...section, blocks: [...others, ...changed] }
  return { sections }
}

function sortedRegion(section: LayoutSection, region: string): LayoutBlock[] {
  return section.blocks
    .filter((block) => block.region === region)
    .sort((a, b) => a.position - b.position)
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
