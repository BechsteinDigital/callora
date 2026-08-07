import { describe, expect, it } from 'vitest'
import {
  blockAt,
  clearBlockBinding,
  readDocument,
  setBlockBinding,
  type LayoutDocument,
} from './layout-document'

function documentWith(config?: Record<string, never>): LayoutDocument {
  return {
    sections: [
      {
        layout: 'single',
        position: 0,
        blocks: [
          { blockId: 'demo.hero', region: 'main', position: 0, config },
          { blockId: 'demo.text', region: 'main', position: 1 },
        ],
      },
    ],
  }
}

describe('readDocument', () => {
  it('macht aus Unsinn ein leeres Dokument statt eines Fehlers', () => {
    // Ein Editor, der am kaputten Dokument abstürzt, ist genau der Editor, mit dem es sich
    // nicht mehr reparieren lässt.
    expect(readDocument(null)).toEqual({ sections: [] })
    expect(readDocument({ sections: 'nein' })).toEqual({ sections: [] })
    expect(readDocument(5)).toEqual({ sections: [] })
  })

  it('wirft eine Sektion ohne Block-Array nicht weg, sondern gibt ihr eine leere Liste', () => {
    const read = readDocument({ sections: [{ layout: 'single', position: 0 }] })

    expect(read.sections).toHaveLength(1)
    expect(read.sections[0].blocks).toEqual([])
  })
})

describe('setBlockBinding', () => {
  it('gibt ein neues Dokument zurück und lässt das alte unberührt', () => {
    // Unveränderlich, damit Vue verlässlich rendert und ein Vergleich mit dem gespeicherten
    // Stand ein Referenzvergleich bleibt.
    const before = documentWith()

    const after = setBlockBinding(
      before,
      { sectionIndex: 0, blockIndex: 0 },
      'title',
      { source: 'static', value: 'Hallo' },
    )

    expect(after).not.toBe(before)
    expect(before.sections[0].blocks[0].config).toBeUndefined()
    expect(after.sections[0].blocks[0].config).toEqual({
      title: { source: 'static', value: 'Hallo' },
    })
  })

  it('lässt die Nachbarn in Ruhe', () => {
    const before = documentWith()

    const after = setBlockBinding(
      before,
      { sectionIndex: 0, blockIndex: 0 },
      'title',
      { source: 'static', value: 'Hallo' },
    )

    expect(after.sections[0].blocks[1]).toBe(before.sections[0].blocks[1])
  })

  it('tut nichts bei einer Adresse, die es nicht gibt', () => {
    // Entsteht, wenn ein Panel noch offen ist, während das Dokument darunter kürzer geworden
    // ist. Dann nichts zu tun ist richtiger, als an eine andere Stelle zu schreiben, die
    // zufällig denselben Index hat.
    const before = documentWith()

    const after = setBlockBinding(
      before,
      { sectionIndex: 0, blockIndex: 9 },
      'title',
      { source: 'static', value: 'Hallo' },
    )

    expect(after).toBe(before)
  })
})

describe('clearBlockBinding', () => {
  it('nimmt die Bindung heraus, statt sie auf den Default-Wert zu setzen', () => {
    // Ein entferntes Control folgt dem Block, wenn dessen Autor den Default ändert; ein
    // eingefrorener Wert tut das nicht. Das ist der ganze Unterschied.
    const before = setBlockBinding(
      documentWith(),
      { sectionIndex: 0, blockIndex: 0 },
      'title',
      { source: 'static', value: 'Hallo' },
    )

    const after = clearBlockBinding(before, { sectionIndex: 0, blockIndex: 0 }, 'title')

    expect(after.sections[0].blocks[0].config).toEqual({})
    expect('title' in (after.sections[0].blocks[0].config ?? {})).toBe(false)
  })

  it('lässt das Dokument unverändert, wenn es nichts zu entfernen gibt', () => {
    const before = documentWith()

    expect(clearBlockBinding(before, { sectionIndex: 0, blockIndex: 0 }, 'title')).toBe(before)
  })
})

describe('blockAt', () => {
  it('findet den Block und verträgt eine leere Auswahl', () => {
    const document = documentWith()

    expect(blockAt(document, { sectionIndex: 0, blockIndex: 1 })?.blockId).toBe('demo.text')
    expect(blockAt(document, null)).toBeUndefined()
    expect(blockAt(document, { sectionIndex: 4, blockIndex: 0 })).toBeUndefined()
  })
})
