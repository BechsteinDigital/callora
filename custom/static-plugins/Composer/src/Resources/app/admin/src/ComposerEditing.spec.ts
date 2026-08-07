import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { h } from 'vue'
import ComposerAdminPage from './ComposerAdminPage.vue'

/**
 * Der Weg, den ein Redakteur wirklich geht: Layout laden, Block anklicken, Einstellung ändern,
 * Autosave. Was hier bricht, bricht in der Benutzung — nicht in einer Hilfsfunktion.
 */
const loadSurfaceBundles = vi.fn()
vi.mock('@callora/surface', () => ({
  loadSurfaceBundles: (...args: unknown[]) => loadSurfaceBundles(...args),
  surfaceBaseTokens: ':root { --cal-color-fg: #111; --cal-color-bg: #fff; --cal-space-4: 1rem }',
}))

// Inline statt über eine äußere Konstante: Die Factory läuft beim ersten Import des gemockten
// Moduls, also bevor die Konstanten dieser Datei ausgewertet sind.
vi.mock('./preview-assets', () => ({
  fetchSurfaceStyles: async () => '',
  fetchTheme: async () => ({
    valuesByKey: {},
    sectionLayouts: [
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
    ],
  }),
}))

const HERO = {
  id: 'demo.hero',
  label: 'Hero',
  category: 'content',
  component: { render: () => h('div', { class: 'hero' }, [h('button', 'Mehr')]) },
  controls: {
    title: { type: 'text', label: 'Titel', default: 'Standard' },
    farbe: { type: 'colorToken', label: 'Farbe' },
  },
}

const DOCUMENT = {
  sections: [
    {
      layout: 'single',
      position: 0,
      blocks: [{ blockId: 'demo.hero', region: 'main', position: 0 }],
    },
  ],
}

/** Die Anfragen, die die Seite abgesetzt hat — Reihenfolge und Rümpfe sind die Aussage. */
let requests: { url: string; init?: RequestInit }[] = []
let saveResponses: { status: number; changedAtUtc?: string }[] = []

function stubFetch(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      requests.push({ url, init })
      if (init?.method === 'PUT') {
        const next = saveResponses.shift() ?? { status: 200, changedAtUtc: '2026-08-07T00:00:01Z' }
        return {
          ok: next.status < 400,
          status: next.status,
          json: async () => ({ changedAtUtc: next.changedAtUtc }),
        }
      }

      if (url.endsWith('/composer/pages')) {
        return {
          ok: true,
          status: 200,
          json: async () => [
            { surfaceKey: 'portal', label: 'Portal', parentSurfaceKey: null, position: 0,
              layoutKey: 'portal', hasPublishedVersion: true },
            { surfaceKey: 'kunden', label: 'Kunden', parentSurfaceKey: 'portal', position: 0,
              layoutKey: 'kunden', hasPublishedVersion: false },
          ],
        }
      }

      if (url.includes('/composer/pages/')) {
        return { ok: true, status: init?.method === 'DELETE' ? 204 : 200, json: async () => ({}) }
      }

      if (url.endsWith('/composer/layouts')) {
        return {
          ok: true,
          status: 200,
          json: async () => [
            { layoutKey: 'portal', name: 'Portal', surfaceKey: 'portal', hasPublishedVersion: true },
          ],
        }
      }

      return {
        ok: true,
        status: 200,
        json: async () => ({
          layoutKey: 'portal',
          workspaceKey: 'acme',
          surfaceKey: 'portal',
          versionNumber: 1,
          document: DOCUMENT,
          changedAtUtc: '2026-08-07T00:00:00Z',
        }),
      }
    }),
  )
}

beforeEach(() => {
  vi.useFakeTimers()
  requests = []
  saveResponses = []
  loadSurfaceBundles.mockReset()
  loadSurfaceBundles.mockResolvedValue({ registry: {}, results: [], styles: [] })
  ;(globalThis as Record<string, unknown>).calloraSurface = { blocks: { blocks: [HERO] } }
  stubFetch()
})

afterEach(() => {
  vi.useRealTimers()
  delete (globalThis as Record<string, unknown>).calloraSurface
})

async function openEditor() {
  const wrapper = mount(ComposerAdminPage)
  await flushPromises()
  await wrapper.find('#composer-layout-key').setValue('portal')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  return wrapper
}

/** Lässt den Autosave-Debounce ablaufen und die Anfrage durchlaufen. */
async function settleAutosave(): Promise<void> {
  await vi.advanceTimersByTimeAsync(1000)
  await flushPromises()
}

const saves = () => requests.filter((request) => request.init?.method === 'PUT')
const savedBody = (index: number) => JSON.parse(String(saves()[index].init?.body))

describe('Die Layout-Auswahl', () => {
  it('bietet die Layouts des Workspaces an, statt den Schlüssel tippen zu lassen', async () => {
    // Wer den Schlüssel tippen muss, muss ihn kennen — und ein Tippfehler sieht aus wie ein
    // fehlendes Layout.
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    const options = wrapper.find('#composer-layout-key').findAll('option').map((o) => o.text())
    expect(options.some((text) => text.includes('Portal'))).toBe(true)
  })

  it('lässt das Textfeld übrig, wenn die Liste nicht geladen werden konnte', async () => {
    // Wer den Schlüssel kennt, soll dann weiterarbeiten können, statt vor einer leeren Auswahl
    // zu stehen.
    vi.stubGlobal('fetch', vi.fn(async () => {
      throw new Error('offline')
    }))
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    expect(wrapper.find('select#composer-layout-key').exists()).toBe(false)
    expect(wrapper.find('input#composer-layout-key').exists()).toBe(true)
  })
})

describe('Der Seitenbaum', () => {
  it('zeigt die Seiten des Workspaces und öffnet eine davon', async () => {
    // Die Gliederung, in der jemand denkt — statt eines Schlüssels, den er kennen muss.
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    const entry = wrapper.findAll('.composer-pages button').find((b) => b.text() === 'Portal')
    expect(entry).toBeDefined()

    await entry!.trigger('click')
    await flushPromises()

    expect(requests.some((request) => request.url.includes('/layouts/portal/draft'))).toBe(true)
  })

  it('lässt eine Seite ohne Erlebniswelt nicht öffnen, sagt aber warum', async () => {
    // Sie ist eine Gliederungsebene, kein Fehler — ohne den Hinweis sähe ein deaktivierter
    // Knopf nach einem Defekt aus.
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url.endsWith('/composer/pages')) {
        return {
          ok: true,
          status: 200,
          json: async () => [
            { surfaceKey: 'bereich', label: 'Bereich', parentSurfaceKey: null, position: 0,
              layoutKey: null, hasPublishedVersion: false },
          ],
        }
      }

      return { ok: true, status: 200, json: async () => [] }
    }))
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    const entry = wrapper.findAll('.composer-pages button').find((b) => b.text() === 'Bereich')
    expect(entry!.attributes('disabled')).toBeDefined()
    expect(wrapper.find('.composer-pages').text()).toContain('ohne Erlebniswelt')
  })
})

describe('Seiten verwalten', () => {
  it('bietet Verschieben und Löschen nur für Seiten an, nicht für Anwendungswurzeln', async () => {
    // Eine Wurzel trägt Host, Zugangsmodus und Identitätsanbieter — die verwaltet der
    // Workspace, nicht der Editor.
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    const rows = wrapper.findAll('.composer-pages li')
    expect(rows[0].text()).toContain('Portal')
    expect(rows[0].find('.composer-pages__delete').exists()).toBe(false)
    expect(rows[1].find('.composer-pages__delete').exists()).toBe(true)
  })

  it('fragt vor dem Löschen und lädt danach neu', async () => {
    // Die einzige Aktion hier, die sich nicht zurücknehmen lässt.
    vi.stubGlobal('confirm', vi.fn(() => true))
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()
    const before = requests.filter((r) => r.url.endsWith('/composer/pages')).length

    await wrapper.findAll('.composer-pages__delete')[0].trigger('click')
    await flushPromises()

    expect(requests.some((r) => r.init?.method === 'DELETE')).toBe(true)
    expect(requests.filter((r) => r.url.endsWith('/composer/pages')).length).toBeGreaterThan(before)
  })

  it('löscht nichts, wenn die Rückfrage verneint wird', async () => {
    vi.stubGlobal('confirm', vi.fn(() => false))
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    await wrapper.findAll('.composer-pages__delete')[0].trigger('click')
    await flushPromises()

    expect(requests.some((r) => r.init?.method === 'DELETE')).toBe(false)
  })

  it('verschiebt eine Seite unter ein anderes Übergeordnetes', async () => {
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    // Die Auswahl bietet sich selbst nicht an — ein Knoten kann nicht sein eigener Elternteil
    // sein.
    const select = wrapper.findAll('.composer-pages__move')[0]
    const offered = select.findAll('option').map((o) => o.attributes('value'))
    expect(offered).not.toContain('kunden')

    await select.setValue('portal')
    await flushPromises()

    // Unverändert heißt: nichts tun. `kunden` hängt schon unter `portal`.
    expect(requests.some((r) => r.url.includes('/parent'))).toBe(false)
  })
})

describe('Eine Seite anlegen', () => {
  it('legt Knoten und Erlebniswelt in einem an und öffnet sie', async () => {
    // Bisher zwei Schritte in zwei Oberflächen — und wer den zweiten vergaß, hatte einen
    // Knoten, der auf nichts zeigt.
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    await wrapper.find('.composer-pages__add input').setValue('Arbeitsplatz')
    await wrapper.find('.composer-pages__add select').setValue('portal')
    await wrapper.find('.composer-pages__add').trigger('submit')
    await flushPromises()

    const created = requests.find((request) => request.init?.method === 'POST')
    expect(created).toBeDefined()
    const body = JSON.parse(String(created!.init?.body))
    expect(body.parentSurfaceKey).toBe('portal')
    expect(body.label).toBe('Arbeitsplatz')
    // Der Schlüssel entsteht aus dem Namen — ein zweites Feld dafür wäre der häufigste Grund,
    // aus dem ein Anlegen scheitert.
    expect(body.surfaceKey).toBe('arbeitsplatz')
  })

  it('macht aus Umlauten und Leerzeichen einen brauchbaren Schlüssel', async () => {
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    await wrapper.find('.composer-pages__add input').setValue('Über uns & Kontakt')
    await wrapper.find('.composer-pages__add select').setValue('portal')
    await wrapper.find('.composer-pages__add').trigger('submit')
    await flushPromises()

    const body = JSON.parse(String(requests.find((r) => r.init?.method === 'POST')!.init?.body))
    expect(body.surfaceKey).toBe('uber-uns-kontakt')
  })

  it('legt nichts an, solange kein Übergeordnetes gewählt ist', async () => {
    // Eine Anwendungswurzel trägt Host, Zugangsmodus und Identitätsanbieter — die legt die
    // Workspace-Verwaltung an, nicht der Editor.
    const wrapper = mount(ComposerAdminPage)
    await flushPromises()

    await wrapper.find('.composer-pages__add input').setValue('Arbeitsplatz')
    await wrapper.find('.composer-pages__add').trigger('submit')
    await flushPromises()

    expect(requests.some((request) => request.init?.method === 'POST')).toBe(false)
  })
})

describe('Veröffentlichen und Verwerfen', () => {
  it('speichert erst, dann veröffentlicht es', async () => {
    // Sonst ginge ein Stand live, der die letzte Änderung nicht enthält — und niemand sähe,
    // dass etwas fehlt.
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')
    await wrapper.find('#control-title').setValue('Neu')

    await wrapper.findAll('button').find((b) => b.text() === 'Veröffentlichen')!.trigger('click')
    await flushPromises()

    const methods = requests.map((request) => `${request.init?.method ?? 'GET'} ${request.url}`)
    const save = methods.findIndex((entry) => entry.startsWith('PUT'))
    const publish = methods.findIndex((entry) => entry.includes('/publish'))
    expect(save).toBeGreaterThanOrEqual(0)
    expect(publish).toBeGreaterThan(save)
  })

  it('veröffentlicht nicht, wenn das Speichern in einen Konflikt lief', async () => {
    // Der fremde Stand wäre sonst das, was live geht.
    saveResponses = [{ status: 409 }]
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')
    await wrapper.find('#control-title').setValue('Neu')

    await wrapper.findAll('button').find((b) => b.text() === 'Veröffentlichen')!.trigger('click')
    await flushPromises()

    expect(requests.some((request) => request.url.includes('/publish'))).toBe(false)
  })

  it('lädt nach dem Verwerfen neu', async () => {
    // Verwerfen ersetzt den Entwurf durch den veröffentlichten Stand. Weiter gegen den alten
    // Stempel zu speichern gäbe einen Konflikt gegen sich selbst.
    const wrapper = await openEditor()
    const before = requests.filter((request) => request.url.includes('/draft')).length

    await wrapper.findAll('button').find((b) => b.text() === 'Verwerfen')!.trigger('click')
    await flushPromises()

    expect(requests.some((request) => request.url.includes('/discard'))).toBe(true)
    expect(requests.filter((r) => r.url.includes('/draft')).length).toBeGreaterThan(before)
  })
})

describe('Blöcke auswählen und einstellen', () => {
  it('öffnet beim Klick auf einen Block sein generiertes Panel', async () => {
    const wrapper = await openEditor()

    expect(wrapper.find('.composer-inspector').exists()).toBe(false)
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    // Generiert heißt: Was im Panel steht, kommt aus `controls` des Blocks und aus nichts
    // sonst. Ein handgebautes Panel wäre eine zweite Beschreibung derselben Einstellungen.
    expect(wrapper.find('.composer-inspector').text()).toContain('Titel')
    expect(wrapper.find('#control-title').exists()).toBe(true)
  })

  it('bietet der Farb-Auswahl nur Rollen an, die im Canvas gelten', async () => {
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    const options = wrapper.find('#control-farbe').findAll('option').map((o) => o.attributes('value'))

    expect(options).toEqual(['', 'color-bg', 'color-fg'])
    // `space-4` ist eine Rolle, aber keine Farbe. Sie hier anzubieten hieße, den Guardrail auf
    // die Namensgleichheit zu reduzieren.
    expect(options).not.toContain('space-4')
  })

  it('schreibt eine Änderung als statische Bindung ins Dokument und speichert sie', async () => {
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    await wrapper.find('#control-title').setValue('Neuer Titel')
    await settleAutosave()

    expect(saves()).toHaveLength(1)
    const body = savedBody(0)
    expect(body.expectedChangedAtUtc).toBe('2026-08-07T00:00:00Z')
    expect(body.document.sections[0].blocks[0].config.title).toEqual({
      source: 'static',
      value: 'Neuer Titel',
    })
  })

  it('speichert nichts, wenn nur geöffnet wurde', async () => {
    // Ohne diesen Vergleich zählte auch das Laden als Änderung, und der Editor schriebe direkt
    // nach dem Öffnen dasselbe zurück — mit neuem Änderungsstempel. Zwei Leute, die eine Seite
    // nur ANSEHEN, gäben sich damit gegenseitig einen Konflikt.
    await openEditor()
    await settleAutosave()

    expect(saves()).toHaveLength(0)
  })

  it('verliert eine Änderung nicht, die während des Speicherns entsteht', async () => {
    // Gespeichert gilt, WAS gesendet wurde. Würde stattdessen der aktuelle Stand als
    // gespeichert vermerkt, fiele die Änderung dazwischen durch das Raster und der nächste
    // Autosave bliebe aus.
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    await wrapper.find('#control-title').setValue('Erst')
    await settleAutosave()
    await wrapper.find('#control-title').setValue('Dann')
    await settleAutosave()

    expect(savedBody(saves().length - 1).document.sections[0].blocks[0].config.title.value)
      .toBe('Dann')
  })

  it('speichert beim zweiten Mal mit dem Stempel aus der ersten Antwort', async () => {
    // Der Fehler, den das verhindert: Nach dem ersten Schreiben ist der eigene Stempel
    // veraltet. Ohne den neuen aus der Antwort liefe der zweite Autosave in einen Konflikt
    // mit sich selbst — die verwirrendste Art zu scheitern, weil niemand sonst etwas anfasste.
    saveResponses = [
      { status: 200, changedAtUtc: '2026-08-07T00:00:05Z' },
      { status: 200, changedAtUtc: '2026-08-07T00:00:09Z' },
    ]
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    await wrapper.find('#control-title').setValue('Erst')
    await settleAutosave()
    await wrapper.find('#control-title').setValue('Dann')
    await settleAutosave()

    expect(saves()).toHaveLength(2)
    expect(savedBody(1).expectedChangedAtUtc).toBe('2026-08-07T00:00:05Z')
  })

  it('hält bei einem Konflikt an, statt weiter zu überschreiben', async () => {
    // Automatisch neu zu laden verlöre die eigene Arbeit, automatisch weiterzuspeichern die
    // des anderen. Beides ist eine Entscheidung, die der Editor nicht treffen darf.
    saveResponses = [{ status: 409 }]
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    await wrapper.find('#control-title').setValue('Erst')
    await settleAutosave()
    await wrapper.find('#control-title').setValue('Dann')
    await settleAutosave()

    expect(saves()).toHaveLength(1)
    expect(wrapper.text()).toContain('Jemand anderes hat diesen Entwurf inzwischen geändert')
  })

  it('nimmt eine Bindung heraus, statt den Default-Wert einzufrieren', async () => {
    // Ein entferntes Control folgt dem Block, wenn dessen Autor den Default ändert. Ein
    // eingefrorener Wert tut das nicht.
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')
    await wrapper.find('#control-title').setValue('Neuer Titel')

    await wrapper.find('.composer-inspector__reset').trigger('click')
    await settleAutosave()

    const body = savedBody(saves().length - 1)
    expect(body.document.sections[0].blocks[0].config).toEqual({})
  })

  it('markiert den ausgewählten Block, damit sichtbar ist, was das Panel meint', async () => {
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    expect(wrapper.find('[data-cal-editor-selected="true"]').exists()).toBe(true)
  })
})

describe('Sektionslayouts aus dem Theme', () => {
  it('bietet ausschließlich an, was das Theme deklariert', async () => {
    // Der Guardrail aus §7.7: Sektionslayouts kommen aus dem Theme, nicht aus dem Editor. So
    // bleibt die Token-Achse die Design-Autorität, und es steht kein Layout-Name im Core.
    const wrapper = await openEditor()

    const offered = wrapper
      .find('#composer-new-section')
      .findAll('option')
      .map((option) => option.attributes('value'))

    expect(offered).toEqual(['', 'single', 'two-2-1'])
  })

  it('legt eine Sektion mit dem gewählten Layout an und speichert sie', async () => {
    const wrapper = await openEditor()

    await wrapper.find('#composer-new-section').setValue('two-2-1')
    await settleAutosave()

    const body = savedBody(saves().length - 1)
    expect(body.document.sections).toHaveLength(2)
    expect(body.document.sections[1]).toMatchObject({ layout: 'two-2-1', position: 1, blocks: [] })
  })

  it('zeigt jede Region des Layouts, auch die leere', async () => {
    // Ohne die leere Region gäbe es keinen Ort, an den sich etwas ziehen ließe — und eine
    // zweispaltige Sektion mit leerer Spalte sähe aus wie eine einspaltige.
    const wrapper = await openEditor()
    await wrapper.find('#composer-new-section').setValue('two-2-1')

    const regions = wrapper.findAll('[data-cal-region]').map((el) => el.attributes('data-cal-region'))

    expect(regions).toContain('main')
    expect(regions).toContain('aside')
  })

  it('behält beim Layout-Wechsel die Blöcke, wo sie sind', async () => {
    // Der Block liegt in `aside`, und `single` hat diese Region nicht. Ihn nach `main`
    // umzuhängen wäre die scheinbar hilfreiche Variante und die, die Arbeit vernichtet: Wer
    // zurückwechselt, fände seine Seitenspalte im Hauptbereich wieder.
    DOCUMENT.sections[0].layout = 'two-2-1'
    DOCUMENT.sections[0].blocks[0].region = 'aside'
    try {
      const wrapper = await openEditor()

      await wrapper.find('#composer-section-0').setValue('single')
      await settleAutosave()

      const body = savedBody(saves().length - 1)
      expect(body.document.sections[0].layout).toBe('single')
      expect(body.document.sections[0].blocks[0].region).toBe('aside')
    } finally {
      DOCUMENT.sections[0].layout = 'single'
      DOCUMENT.sections[0].blocks[0].region = 'main'
    }
  })
})

describe('Wenn das Theme ein Layout nicht mehr kennt', () => {
  it('nennt die gestrandeten Sektionen, statt sie nur anders aussehen zu lassen', async () => {
    // Serverseitig fallen sie beim Rendern auf `single` zurück. Hier zu zeigen, WELCHE es
    // trifft, ist der Unterschied zwischen „meine Seite sieht anders aus" und „diese Sektion
    // hängt an einem Layout, das das neue Theme nicht mitbringt".
    DOCUMENT.sections[0].layout = 'drei-spalten'
    try {
      const wrapper = await openEditor()

      expect(wrapper.text()).toContain('drei-spalten')
      expect(wrapper.text()).toContain('einspaltig ausgeliefert')
    } finally {
      DOCUMENT.sections[0].layout = 'single'
    }
  })

  it('lässt das unbekannte Layout in der Auswahl stehen', async () => {
    // Sonst zeigte die Auswahl etwas anderes an, als im Dokument steht, und der nächste Klick
    // irgendwohin änderte still das Layout.
    DOCUMENT.sections[0].layout = 'drei-spalten'
    try {
      const wrapper = await openEditor()

      const options = wrapper
        .find('#composer-section-0')
        .findAll('option')
        .map((option) => option.attributes('value'))

      expect(options).toContain('drei-spalten')
      expect((wrapper.find('#composer-section-0').element as HTMLSelectElement).value)
        .toBe('drei-spalten')
    } finally {
      DOCUMENT.sections[0].layout = 'single'
    }
  })
})

describe('Blöcke ziehen und ablegen', () => {
  /** Ein DataTransfer-Ersatz: happy-dom liefert bei synthetischen Events keinen. */
  function transfer(payload?: unknown) {
    const store = new Map<string, string>()
    if (payload !== undefined) {
      store.set('application/x-callora-block', JSON.stringify(payload))
    }

    return {
      effectAllowed: '',
      setData: (format: string, data: string) => store.set(format, data),
      getData: (format: string) => store.get(format) ?? '',
    }
  }

  it('bietet die registrierten Blöcke in der Palette an', async () => {
    const wrapper = await openEditor()

    expect(wrapper.find('.composer-palette').text()).toContain('Hero')
    expect(wrapper.find('[data-block-id="demo.hero"]').exists()).toBe(true)
  })

  it('zeigt Ablegezonen erst, WÄHREND gezogen wird', async () => {
    // Sie sind echte Elemente zwischen den Blöcken. Dauerhaft eingefügt bräche jede `+`- und
    // `>`-Regel des Themes — im Ruhezustand muss der Baum der der Fläche sein.
    const wrapper = await openEditor()

    expect(wrapper.findAll('.cal-canvas__dropzone')).toHaveLength(0)

    await wrapper.find('[data-block-id="demo.hero"]').trigger('dragstart', {
      dataTransfer: transfer(),
    })

    expect(wrapper.findAll('.cal-canvas__dropzone').length).toBeGreaterThan(0)

    await wrapper.find('.composer__workspace').trigger('dragend')

    expect(wrapper.findAll('.cal-canvas__dropzone')).toHaveLength(0)
  })

  it('setzt einen Block aus der Palette an die abgelegte Stelle und speichert ihn', async () => {
    const wrapper = await openEditor()
    await wrapper.find('[data-block-id="demo.hero"]').trigger('dragstart', {
      dataTransfer: transfer(),
    })

    // Die Zone hinter dem einen vorhandenen Block der Region `main`.
    const zone = wrapper.find('[data-cal-drop-region="main"][data-cal-drop-index="1"]')
    await zone.trigger('drop', { dataTransfer: transfer({ kind: 'new', blockId: 'demo.hero' }) })
    await settleAutosave()

    const blocks = savedBody(saves().length - 1).document.sections[0].blocks
    expect(blocks).toHaveLength(2)
    expect(blocks.map((b: { position: number }) => b.position).sort()).toEqual([0, 1])
  })

  it('bewirkt nichts, wenn die abgelegte Nutzlast nicht von hier stammt', async () => {
    // Ein Drop kann aus einem anderen Fenster, einem Editor oder einem Dateimanager kommen.
    const wrapper = await openEditor()
    await wrapper.find('[data-block-id="demo.hero"]').trigger('dragstart', {
      dataTransfer: transfer(),
    })

    await wrapper
      .find('[data-cal-drop-region="main"]')
      .trigger('drop', { dataTransfer: transfer() })
    await settleAutosave()

    expect(saves()).toHaveLength(0)
  })

  it('legt einen Block in eine leere Region der zweiten Spalte', async () => {
    DOCUMENT.sections[0].layout = 'two-2-1'
    try {
      const wrapper = await openEditor()
      await wrapper.find('[data-block-id="demo.hero"]').trigger('dragstart', {
        dataTransfer: transfer(),
      })

      await wrapper
        .find('[data-cal-drop-region="aside"]')
        .trigger('drop', { dataTransfer: transfer({ kind: 'new', blockId: 'demo.hero' }) })
      await settleAutosave()

      const blocks = savedBody(saves().length - 1).document.sections[0].blocks
      expect(blocks.some((b: { region: string }) => b.region === 'aside')).toBe(true)
    } finally {
      DOCUMENT.sections[0].layout = 'single'
    }
  })

  it('entfernt den ausgewählten Block', async () => {
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    await wrapper.find('.composer-inspector__remove').trigger('click')
    await settleAutosave()

    expect(savedBody(saves().length - 1).document.sections[0].blocks).toEqual([])
    // Das Panel zeigte sonst auf etwas, das es nicht mehr gibt.
    expect(wrapper.find('.composer-inspector').exists()).toBe(false)
  })
})

describe('Der Klick-Konflikt', () => {
  it('steht im Editier-Modus auf abfangen und schaltet um', async () => {
    // Der Umschalter ändert, wie ein Block auf Zeigereingaben reagiert — die CSS-Regel hängt
    // an diesem Attribut. Ohne es feuerte ein Button IM Block, statt den Block auszuwählen.
    const wrapper = await openEditor()

    expect(wrapper.find('.cal-canvas').attributes('data-cal-editing')).toBe('true')

    await wrapper.find('.composer__modes input[type="checkbox"]').setValue(false)

    expect(wrapper.find('.cal-canvas').attributes('data-cal-editing')).toBe('false')
  })

  it('behält die Auswahl, wenn man interaktiv testet', async () => {
    // Wer ein Akkordeon aufklappen will, um zu sehen, was darin steht, soll dafür nicht die
    // Auswahl verlieren.
    const wrapper = await openEditor()
    await wrapper.find('[data-cal-editor-block]').trigger('click')

    await wrapper.find('.composer__modes input[type="checkbox"]').setValue(false)

    expect(wrapper.find('.composer-inspector').exists()).toBe(true)
    expect(wrapper.find('[data-cal-editor-selected="true"]').exists()).toBe(true)
  })
})
