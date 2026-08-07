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

vi.mock('./preview-assets', () => ({
  fetchSurfaceStyles: async () => '',
  fetchThemeTokens: async () => ({}),
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
