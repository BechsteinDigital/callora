import { describe, expect, it } from 'vitest'
import type { SectionLayout } from './preview-assets'
import type { LayoutDocument } from './layout-document'
import { regionsOf, sectionsWithUnknownLayout, themeDeclaresLayouts } from './section-layouts'

const LAYOUTS: SectionLayout[] = [
  {
    layoutKey: 'single',
    label: 'Eine Spalte',
    regions: [{ regionKey: 'main', label: 'Inhalt' }],
    sortOrder: 10,
  },
  {
    layoutKey: 'two-2-1',
    label: 'Zwei Spalten',
    regions: [
      { regionKey: 'main', label: 'Inhalt' },
      { regionKey: 'aside', label: 'Seitenspalte' },
    ],
    sortOrder: 20,
  },
]

function documentWith(layout: string, regions: string[] = []): LayoutDocument {
  return {
    sections: [
      {
        layout,
        position: 0,
        blocks: regions.map((region, index) => ({
          blockId: `demo.${region}`,
          region,
          position: index,
        })),
      },
    ],
  }
}

describe('regionsOf', () => {
  it('gibt alle Regionen des Layouts, auch die leeren', () => {
    // Eine leere Region muss sichtbar sein, sonst gibt es keinen Ort, an den man etwas ziehen
    // könnte — und eine zweispaltige Sektion mit leerer Spalte sähe aus wie eine einspaltige.
    const [section] = documentWith('two-2-1', ['main']).sections

    expect(regionsOf(section, LAYOUTS).map((region) => region.regionKey)).toEqual(['main', 'aside'])
  })

  it('behält die Reihenfolge des Themes', () => {
    const [section] = documentWith('two-2-1').sections

    // Alphabetisch stünde "aside" vor "main" — die Seitenspalte vor dem Inhalt, neben dem sie
    // sitzt.
    expect(regionsOf(section, LAYOUTS)[0].regionKey).toBe('main')
  })

  it('hängt Blöcke aus einer nicht deklarierten Region hinten an, statt sie zu verstecken', () => {
    // Ihre Blöcke stehen weiter im Dokument und kommen zurück, sobald das Theme die Region
    // wieder mitbringt. Wegzulassen hieße, Inhalt zu verstecken, den es noch gibt.
    const [section] = documentWith('single', ['main', 'footer']).sections

    const regions = regionsOf(section, LAYOUTS)

    expect(regions.map((region) => region.regionKey)).toEqual(['main', 'footer'])
    expect(regions.map((region) => region.declared)).toEqual([true, false])
  })

  it('zeigt bei unbekanntem Layout die benutzten Regionen', () => {
    // Das Theme kennt das Layout nicht, also gibt es keine deklarierten Regionen. Nichts zu
    // zeigen hieße, die Sektion sähe leer aus, obwohl Blöcke darin stehen.
    const [section] = documentWith('erfunden', ['main']).sections

    expect(regionsOf(section, LAYOUTS).map((region) => region.regionKey)).toEqual(['main'])
  })
})

describe('sectionsWithUnknownLayout', () => {
  it('findet die Sektionen, die nach einem Theme-Wechsel gestrandet sind', () => {
    const document: LayoutDocument = {
      sections: [
        { layout: 'single', position: 0, blocks: [] },
        { layout: 'drei-spalten', position: 1, blocks: [] },
      ],
    }

    const stranded = sectionsWithUnknownLayout(document, LAYOUTS)

    expect(stranded.map((entry) => entry.index)).toEqual([1])
  })

  it('warnt zu keiner Sektion, wenn das Theme gar keine Layouts deklariert', () => {
    // Zwei Fälle sehen gleich aus und sind es nicht: „kennt dieses Layout nicht mehr" ist eine
    // Ablehnung, „deklariert überhaupt keine" ist keine Aussage. Hier zu warnen wäre eine
    // Warnung aus einem Nicht-Ereignis.
    const document = documentWith('was-auch-immer')

    expect(sectionsWithUnknownLayout(document, [])).toEqual([])
  })
})

describe('themeDeclaresLayouts', () => {
  it('unterscheidet ein Theme mit Layouts von einem ohne', () => {
    expect(themeDeclaresLayouts(LAYOUTS)).toBe(true)
    expect(themeDeclaresLayouts([])).toBe(false)
  })
})
