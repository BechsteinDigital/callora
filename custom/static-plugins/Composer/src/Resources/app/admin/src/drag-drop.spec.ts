import { describe, expect, it } from 'vitest'
import type { BlockCategory, BlockDefinition } from '@callora/surface'
import { h } from 'vue'
import {
  indexInRegion,
  insertBlock,
  moveBlock,
  removeBlock,
  type LayoutDocument,
} from './layout-document'
import { isOfferedOnSurface, paletteGroups, readDragPayload } from './block-palette'

/** Ein Dokument mit einer zweispaltigen Sektion: main a,b,c — aside x. */
function documentWith(): LayoutDocument {
  return {
    sections: [
      {
        layout: 'two-2-1',
        position: 0,
        blocks: [
          { blockId: 'a', region: 'main', position: 0 },
          { blockId: 'b', region: 'main', position: 1 },
          { blockId: 'c', region: 'main', position: 2 },
          { blockId: 'x', region: 'aside', position: 0 },
        ],
      },
    ],
  }
}

/** Die Blöcke einer Region in Anzeigereihenfolge. */
function order(document: LayoutDocument, region: string): string[] {
  return document.sections[0].blocks
    .filter((block) => block.region === region)
    .sort((a, b) => a.position - b.position)
    .map((block) => block.blockId)
}

/** Der Adress-Index eines Blocks im `blocks`-Array — was das Ziehen mitgibt. */
function addressOf(document: LayoutDocument, blockId: string): number {
  return document.sections[0].blocks.findIndex((block) => block.blockId === blockId)
}

describe('insertBlock', () => {
  it('setzt an die gewählte Stelle und nummeriert die Region neu durch', () => {
    // `position` ist eine Reihenfolge, keine Identität. Neu zu nummerieren ändert mehr Zahlen
    // als nötig und macht ein Dokument mit Lücken oder Dubletten — aus einem Rückrollen, einem
    // fremden Editor — wieder berechenbar, statt es Schritt für Schritt unberechenbarer werden
    // zu lassen.
    const after = insertBlock(documentWith(), { sectionIndex: 0, region: 'main', index: 1 }, 'neu')

    expect(order(after, 'main')).toEqual(['a', 'neu', 'b', 'c'])
    expect(
      after.sections[0].blocks.filter((b) => b.region === 'main').map((b) => b.position).sort(),
    ).toEqual([0, 1, 2, 3])
  })

  it('setzt ans Ende und in eine leere Region', () => {
    const atEnd = insertBlock(documentWith(), { sectionIndex: 0, region: 'main', index: 3 }, 'neu')
    expect(order(atEnd, 'main')).toEqual(['a', 'b', 'c', 'neu'])

    const inEmpty = insertBlock(documentWith(), { sectionIndex: 0, region: 'leer', index: 0 }, 'neu')
    expect(order(inEmpty, 'leer')).toEqual(['neu'])
  })

  it('lässt die anderen Regionen unberührt', () => {
    const after = insertBlock(documentWith(), { sectionIndex: 0, region: 'main', index: 0 }, 'neu')

    expect(order(after, 'aside')).toEqual(['x'])
  })

  it('tut nichts bei einer Sektion, die es nicht gibt', () => {
    const before = documentWith()

    expect(insertBlock(before, { sectionIndex: 7, region: 'main', index: 0 }, 'neu')).toBe(before)
  })
})

describe('moveBlock', () => {
  it('verschiebt innerhalb der Region nach unten, ohne eine Stelle zu verlieren', () => {
    // Der Fall, den eine naive Umsetzung falsch macht: Wird zuerst eingesetzt und dann
    // herausgenommen — oder das Ziel nicht korrigiert —, zählt die Zielstelle den Block noch
    // mit, und „eins weiter unten" landet genau dort, wo er war.
    const before = documentWith()

    const after = moveBlock(
      before,
      { sectionIndex: 0, blockIndex: addressOf(before, 'a') },
      { sectionIndex: 0, region: 'main', index: 2 },
    )

    expect(order(after, 'main')).toEqual(['b', 'a', 'c'])
  })

  it('verschiebt innerhalb der Region nach oben', () => {
    const before = documentWith()

    const after = moveBlock(
      before,
      { sectionIndex: 0, blockIndex: addressOf(before, 'c') },
      { sectionIndex: 0, region: 'main', index: 0 },
    )

    expect(order(after, 'main')).toEqual(['c', 'a', 'b'])
  })

  it('verschiebt in eine andere Region und nimmt die Einstellungen mit', () => {
    // Verschoben, nicht neu erzeugt: Alles, was jemand am Block eingestellt hat, zieht mit —
    // auch über die Regionsgrenze.
    const before = documentWith()
    before.sections[0].blocks[0].config = { title: { source: 'static', value: 'Hallo' } }

    const after = moveBlock(
      before,
      { sectionIndex: 0, blockIndex: addressOf(before, 'a') },
      { sectionIndex: 0, region: 'aside', index: 0 },
    )

    expect(order(after, 'main')).toEqual(['b', 'c'])
    expect(order(after, 'aside')).toEqual(['a', 'x'])
    const moved = after.sections[0].blocks.find((block) => block.blockId === 'a')
    expect(moved?.config).toEqual({ title: { source: 'static', value: 'Hallo' } })
    expect(moved?.region).toBe('aside')
  })

  it('lässt eine Verschiebung an dieselbe Stelle die Reihenfolge unverändert', () => {
    const before = documentWith()

    const after = moveBlock(
      before,
      { sectionIndex: 0, blockIndex: addressOf(before, 'b') },
      { sectionIndex: 0, region: 'main', index: 1 },
    )

    expect(order(after, 'main')).toEqual(['a', 'b', 'c'])
  })

  it('tut nichts über Sektionsgrenzen hinweg', () => {
    // Noch nicht vorgesehen. Stillschweigend in die falsche Sektion zu schreiben wäre
    // schlimmer, als nichts zu tun.
    const before = documentWith()

    const after = moveBlock(
      before,
      { sectionIndex: 0, blockIndex: 0 },
      { sectionIndex: 1, region: 'main', index: 0 },
    )

    expect(after).toBe(before)
  })
})

describe('removeBlock', () => {
  it('nimmt den Block heraus und schließt die Lücke in den Positionen', () => {
    const before = documentWith()

    const after = removeBlock(before, { sectionIndex: 0, blockIndex: addressOf(before, 'b') })

    expect(order(after, 'main')).toEqual(['a', 'c'])
    expect(
      after.sections[0].blocks.filter((b) => b.region === 'main').map((b) => b.position),
    ).toEqual([0, 1])
  })

  it('tut nichts bei einer Adresse, die es nicht gibt', () => {
    const before = documentWith()

    expect(removeBlock(before, { sectionIndex: 0, blockIndex: 99 })).toBe(before)
  })
})

describe('indexInRegion', () => {
  it('zählt innerhalb der Region, nicht über die Sektion', () => {
    // Der Adress-Index zählt regionenübergreifend und in Speicherreihenfolge; beim Ziehen
    // sichtbar ist die Stelle in der Region.
    const document = documentWith()

    expect(indexInRegion(document, { sectionIndex: 0, blockIndex: addressOf(document, 'x') })).toBe(0)
    expect(indexInRegion(document, { sectionIndex: 0, blockIndex: addressOf(document, 'c') })).toBe(2)
  })
})

// ── Die Palette ──────────────────────────────────────────────────────────────

function block(id: string, category: string, surfaces?: ('surface' | 'admin')[]): BlockDefinition {
  return { id, label: id, category, component: { render: () => h('div') }, surfaces }
}

describe('isOfferedOnSurface', () => {
  it('bietet einen flächenneutralen Block an', () => {
    expect(isOfferedOnSurface(block('a', 'content'))).toBe(true)
    expect(isOfferedOnSurface(block('a', 'content', []))).toBe(true)
  })

  it('hält einen Block heraus, der ausdrücklich nur den Admin kann', () => {
    // Angeboten würde er platziert und erschiene auf der veröffentlichten Seite nie, ohne dass
    // jemand erführe, warum.
    expect(isOfferedOnSurface(block('a', 'content', ['admin']))).toBe(false)
    expect(isOfferedOnSurface(block('a', 'content', ['surface']))).toBe(true)
  })
})

describe('paletteGroups', () => {
  const categories: BlockCategory[] = [
    { id: 'content', label: 'Inhalt', order: 10 },
    { id: 'media', label: 'Medien', order: 20 },
  ]

  it('gruppiert in der Reihenfolge der registrierten Kategorien', () => {
    const groups = paletteGroups(
      [block('bild', 'media'), block('text', 'content')],
      categories,
    )

    expect(groups.map((group) => group.label)).toEqual(['Inhalt', 'Medien'])
  })

  it('lässt eine Kategorie weg, zu der es keinen Block gibt', () => {
    const groups = paletteGroups([block('text', 'content')], categories)

    expect(groups.map((group) => group.categoryId)).toEqual(['content'])
  })

  it('verliert einen Block mit unbekannter Kategorie nicht', () => {
    // Die Registry lässt ihn bewusst durch — er soll nicht an der Ladereihenfolge zweier
    // Plugins scheitern. Ihn hier doch noch zu verlieren wäre dieselbe Strafe an anderer Stelle.
    const groups = paletteGroups([block('text', 'content'), block('exot', 'unbekannt')], categories)

    expect(groups.map((group) => group.categoryId)).toEqual(['content', 'unbekannt'])
    expect(groups[1].label).toBe('unbekannt')
  })

  it('bietet nur an, was auf einer Fläche laufen darf', () => {
    const groups = paletteGroups(
      [block('text', 'content'), block('nurAdmin', 'content', ['admin'])],
      categories,
    )

    expect(groups[0].blocks.map((b) => b.id)).toEqual(['text'])
  })
})

describe('readDragPayload', () => {
  it('liest die beiden Formen', () => {
    expect(readDragPayload('{"kind":"new","blockId":"demo.hero"}')).toEqual({
      kind: 'new',
      blockId: 'demo.hero',
    })
    expect(readDragPayload('{"kind":"move","sectionIndex":0,"blockIndex":2}')).toEqual({
      kind: 'move',
      sectionIndex: 0,
      blockIndex: 2,
    })
  })

  it('weist alles zurück, was nicht passt', () => {
    // Ein Drop kann aus einem anderen Fenster, einem Editor oder einem Dateimanager kommen. Das
    // ist kein Grund, dem Dokument etwas hinzuzufügen.
    expect(readDragPayload(null)).toBeNull()
    expect(readDragPayload('')).toBeNull()
    expect(readDragPayload('kein json')).toBeNull()
    expect(readDragPayload('{"kind":"new"}')).toBeNull()
    expect(readDragPayload('{"kind":"new","blockId":""}')).toBeNull()
    expect(readDragPayload('{"kind":"move","sectionIndex":"0","blockIndex":2}')).toBeNull()
    expect(readDragPayload('{"kind":"etwas-anderes"}')).toBeNull()
  })
})
