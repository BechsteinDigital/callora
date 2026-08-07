import type { SectionLayout } from './preview-assets'
import type { LayoutDocument, LayoutSection } from './layout-document'

/**
 * Wie der Editor mit den Sektionslayouts des Themes umgeht.
 *
 * Die Regeln stehen hier als reine Funktionen, weil sie dieselben sind, die der Server beim
 * Rendern anwendet (§7.8). Wären sie in einer Komponente vergraben, gäbe es zwei Fassungen
 * derselben Entscheidung — und die zweite fiele erst auf der veröffentlichten Seite auf.
 */

/** Worauf eine Sektion zurückfällt, deren Layout das Theme nicht mehr kennt. */
export const FALLBACK_LAYOUT = 'single'

/** Das Layout mit diesem Schlüssel, oder undefined. */
export function findLayout(
  layouts: readonly SectionLayout[],
  layoutKey: string,
): SectionLayout | undefined {
  return layouts.find((layout) => layout.layoutKey === layoutKey)
}

/**
 * Ob das Theme zu Layouts überhaupt etwas sagt.
 *
 * Zwei Fälle sehen sonst gleich aus und sind es nicht: Ein Theme, das `two-2-1` NICHT MEHR
 * kennt, hat es abgelehnt. Ein Theme, das gar keine Layouts deklariert, sagt zu keinem etwas —
 * dann jede Sektion als verwaist zu markieren, wäre eine Warnung aus einem Nicht-Ereignis.
 */
export function themeDeclaresLayouts(layouts: readonly SectionLayout[]): boolean {
  return layouts.length > 0
}

/**
 * Sektionen, deren Layout dieses Theme nicht kennt — was der Editor warnend anzeigt.
 *
 * Serverseitig fallen sie beim Rendern auf `single` zurück; hier sichtbar zu machen, WELCHE es
 * trifft, ist der Unterschied zwischen „meine Seite sieht anders aus" und „diese drei Sektionen
 * hängen an einem Layout, das das neue Theme nicht mitbringt".
 */
export function sectionsWithUnknownLayout(
  document: LayoutDocument,
  layouts: readonly SectionLayout[],
): { index: number; section: LayoutSection }[] {
  if (!themeDeclaresLayouts(layouts)) {
    return []
  }

  return document.sections
    .map((section, index) => ({ index, section }))
    .filter(({ section }) => !findLayout(layouts, section.layout))
}

/**
 * Die Regionen, die eine Sektion anbietet — alle des Layouts, auch die leeren.
 *
 * Eine leere Region muss sichtbar sein, sonst gibt es keinen Ort, an den man etwas ziehen
 * könnte: Der Canvas zeigte bisher nur Regionen, in denen schon ein Block lag, und eine
 * zweispaltige Sektion mit einer leeren Spalte sah aus wie eine einspaltige.
 *
 * Blöcke in einer Region, die es im Layout nicht (mehr) gibt, kommen HINTEN dazu. Sie
 * wegzulassen hieße, Inhalt zu verstecken, den das Dokument noch trägt — und der zurückkommt,
 * sobald das Theme die Region wieder mitbringt.
 */
export function regionsOf(
  section: LayoutSection,
  layouts: readonly SectionLayout[],
): { regionKey: string; label: string; declared: boolean }[] {
  const layout = findLayout(layouts, section.layout)
  const declared = (layout?.regions ?? []).map((region) => ({
    regionKey: region.regionKey,
    label: region.label,
    declared: true,
  }))

  const declaredKeys = new Set(declared.map((region) => region.regionKey))
  const orphaned = [...new Set(section.blocks.map((block) => block.region))]
    .filter((regionKey) => !declaredKeys.has(regionKey))
    .sort()
    .map((regionKey) => ({ regionKey, label: regionKey, declared: false }))

  // Kennt das Theme das Layout gar nicht, gibt es keine deklarierten Regionen — dann sind die
  // benutzten alles, was es gibt, und der Canvas zeigt sie in der Reihenfolge des Dokuments.
  return [...declared, ...orphaned]
}
