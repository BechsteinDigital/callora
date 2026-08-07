<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  loadSurfaceBundles,
  surfaceBaseTokens,
  type Binding,
  type BlockCategory,
  type BlockDefinition,
  type PluginLoadResult,
} from '@callora/surface'
import ComposerCanvas from './ComposerCanvas.vue'
import BlockInspector from './BlockInspector.vue'
import BlockPalette from './BlockPalette.vue'
import { readDragPayload } from './block-palette'
import { fetchSurfaceStyles, fetchTheme, type SectionLayout } from './preview-assets'
import { sectionsWithUnknownLayout, themeDeclaresLayouts } from './section-layouts'
import { collectTokenRoles } from './token-roles'
import {
  addSection,
  blockAt,
  clearBlockBinding,
  emptyDocument,
  insertBlock,
  moveBlock,
  readDocument,
  removeBlock,
  setBlockBinding,
  setSectionLayout,
  type BlockAddress,
  type DropTarget,
  type LayoutDocument,
} from './layout-document'
import './composer.css'

/**
 * Die Composer-Seite in der Admin-Shell.
 *
 * Sie holt den Entwurf über die Admin-API des Plugins — dieselbe Route, die eine
 * Operator-Berechtigung verlangt. Der öffentliche Renderpfad kommt hier nicht hin: Er ruft
 * GetPublishedAsync auf einem anderen Vertrag, und es gibt keinen Parameter, der das eine ins
 * andere verwandelt.
 *
 * Danach lädt sie die Block-Bundles der Fläche, für die das Layout gedacht ist, und zeigt den
 * Canvas mit den echten Komponenten. Wer einen Block anklickt, bekommt sein aus `controls`
 * generiertes Panel; jede Änderung geht als Autosave zurück, mit dem Änderungsstempel.
 */
interface LoadedLayout {
  layoutKey: string
  workspaceKey: string
  surfaceKey: string | null
}

const AUTOSAVE_DELAY_MS = 800

const layoutKey = ref('')
/**
 * Die Layouts dieses Workspaces. Eine Auswahl statt eines Textfelds: Wer den Schlüssel tippen
 * muss, muss ihn kennen — und ein Tippfehler sieht aus wie ein fehlendes Layout.
 */
interface LayoutSummary {
  layoutKey: string
  name: string
  surfaceKey: string | null
  hasPublishedVersion: boolean
}

const layouts = ref<LayoutSummary[]>([])
const layout = ref<LoadedLayout | null>(null)
const document = ref<LayoutDocument>(emptyDocument())
/** Der Stand, der auf dem Server liegt — der Vergleich, der das Laden vom Ändern trennt. */
const savedDocument = ref<LayoutDocument | null>(null)
const changedAtUtc = ref<string | null>(null)
const surfaceCss = ref('')
const tokens = ref<Record<string, string>>({})
const sectionLayouts = ref<SectionLayout[]>([])
const bundleFailures = ref<PluginLoadResult[]>([])
const selected = ref<BlockAddress | null>(null)
const editing = ref(true)
/**
 * Ob gerade etwas gezogen wird. Nur dann gibt es Ablegezonen — sie sind echte Elemente
 * zwischen den Blöcken, und dauerhaft eingefügt bräche jede `+`- und `>`-Regel des Themes.
 */
const dragging = ref(false)
const error = ref<string | null>(null)
const conflict = ref(false)
const saving = ref(false)
const loading = ref(false)
const publishing = ref(false)

/**
 * Die Token-Rollen, aus denen ein Erscheinungs-Control wählen darf — gelesen aus genau dem CSS,
 * das im Canvas gilt. Nicht aus einer gepflegten Liste: Die liefe auseinander, sobald jemand ein
 * Token hinzufügt, und das Panel böte dann Rollen an, die es nicht gibt (oder verschwiege
 * welche, die es gibt).
 */
const canvasCss = computed(() => `${surfaceBaseTokens}\n${surfaceCss.value}`)
const tokenRoles = computed(() => collectTokenRoles(canvasCss.value))

const selectedBlock = computed(() => blockAt(document.value, selected.value))

/**
 * Die Registry, bei jedem Zugriff gelesen statt einmal festgehalten: Ein Plugin-Bundle kann
 * nach dem Mounten laden, und was dann dazukommt, soll auch in der Palette auftauchen.
 */
const registry = computed(
  () =>
    (globalThis as {
      calloraSurface?: { blocks?: { blocks: BlockDefinition[]; categories: BlockCategory[] } }
    }).calloraSurface?.blocks,
)

const availableBlocks = computed(() => registry.value?.blocks ?? [])
const blockCategories = computed(() => registry.value?.categories ?? [])

const selectedDefinition = computed<BlockDefinition | undefined>(() => {
  const blockId = selectedBlock.value?.blockId
  return blockId ? availableBlocks.value.find((block) => block.id === blockId) : undefined
})

/**
 * Holt die Auswahl. Ein Fehler hier lässt das Textfeld übrig, statt die Seite zu blockieren:
 * Wer den Schlüssel kennt, soll weiterarbeiten können.
 */
async function loadLayouts(): Promise<void> {
  try {
    const response = await fetch('/api/ext/admin/composer/layouts', { credentials: 'same-origin' })
    if (response.ok) {
      layouts.value = await response.json()
    }
  } catch {
    layouts.value = []
  }
}

void loadLayouts()

async function load(): Promise<void> {
  if (!layoutKey.value) {
    return
  }

  loading.value = true
  error.value = null
  conflict.value = false
  bundleFailures.value = []
  selected.value = null
  try {
    const response = await fetch(draftUrl(layoutKey.value), { credentials: 'same-origin' })
    if (!response.ok) {
      // Was fehlschlug, sagt die Meldung nicht genauer: 404 kann heißen, dass es das Layout
      // nicht gibt ODER dass diese Person es nicht sehen darf, und diese beiden zu trennen
      // verriete, welche Layouts existieren.
      error.value = 'Der Entwurf konnte nicht geladen werden.'
      return
    }

    const draft = await response.json()
    layout.value = {
      layoutKey: draft.layoutKey,
      workspaceKey: draft.workspaceKey,
      surfaceKey: draft.surfaceKey ?? null,
    }
    published.value = false
    document.value = readDocument(draft.document)
    savedDocument.value = document.value
    changedAtUtc.value = draft.changedAtUtc ?? null

    await loadPreview(layout.value)
    // Nach dem Laden aktualisieren: Ein gerade veröffentlichtes Layout soll in der Auswahl
    // nicht weiter als unveröffentlicht stehen.
    void loadLayouts()
  } catch {
    error.value = 'Der Entwurf konnte nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

/**
 * Lädt, was die Vorschau echt macht: die Block-Bundles der Fläche, deren Stylesheets und die
 * Theme-Werte des Workspace.
 *
 * `injectStyles: false` ist hier keine Feinheit. Ein Flächen-Stylesheet beansprucht Namen wie
 * `.cal-header`, die auf beiden Seiten etwas bedeuten — eingebunden würde es die Admin-Shell
 * UM den Canvas herum umgestalten. Der Text wird stattdessen geholt und gescoped.
 */
async function loadPreview(loaded: LoadedLayout): Promise<void> {
  const bundles = await loadSurfaceBundles({
    workspaceKey: loaded.workspaceKey,
    surfaceKey: loaded.surfaceKey ?? undefined,
    injectStyles: false,
  })

  bundleFailures.value = bundles.results.filter((result) => result.status === 'error')

  const [css, theme] = await Promise.all([
    fetchSurfaceStyles(bundles.styles),
    fetchTheme(loaded.workspaceKey),
  ])
  surfaceCss.value = css
  tokens.value = theme.valuesByKey
  sectionLayouts.value = theme.sectionLayouts
}

/**
 * Sektionen, deren Layout dieses Theme nicht kennt (§7.8).
 *
 * Serverseitig fallen sie beim Rendern auf `single` zurück. Hier zu zeigen, WELCHE es trifft,
 * ist der Unterschied zwischen „meine Seite sieht anders aus" und „diese Sektionen hängen an
 * einem Layout, das das neue Theme nicht mitbringt".
 */
const strandedSections = computed(() =>
  sectionsWithUnknownLayout(document.value, sectionLayouts.value),
)

/** Ob das Theme überhaupt Layouts anbietet — ohne sie lässt sich keine Sektion anlegen. */
const hasLayouts = computed(() => themeDeclaresLayouts(sectionLayouts.value))

function appendSection(layoutKey: string): void {
  document.value = addSection(document.value, layoutKey)
}

function changeSectionLayout(sectionIndex: number, layoutKey: string): void {
  // Die Blöcke bleiben, wo sie sind. Sie umzuhängen wäre die scheinbar hilfreiche Variante und
  // die, die Arbeit vernichtet: Wer ein Layout ausprobiert und zurückwechselt, fände seine
  // Seitenspalte im Hauptbereich wieder.
  document.value = setSectionLayout(document.value, sectionIndex, layoutKey)
  selected.value = null
}

/**
 * Was ein Drop bewirkt: ein neuer Block aus der Palette, oder ein bereits platzierter an eine
 * andere Stelle.
 *
 * Eine Nutzlast, die nicht passt, bewirkt nichts. Ein Drop kann aus einem anderen Fenster, einem
 * Editor oder einem Dateimanager kommen — das ist kein Grund, dem Dokument etwas hinzuzufügen.
 */
function handleDrop(target: DropTarget, data: string): void {
  dragging.value = false
  const payload = readDragPayload(data)
  if (!payload) {
    return
  }

  if (payload.kind === 'new') {
    document.value = insertBlock(document.value, target, payload.blockId)
    selected.value = null
    return
  }

  document.value = moveBlock(
    document.value,
    { sectionIndex: payload.sectionIndex, blockIndex: payload.blockIndex },
    target,
  )
  // Die Auswahl zeigt auf einen Index, den das Umsortieren gerade neu vergeben hat. Sie
  // stehenzulassen hieße, das Panel eines anderen Blocks anzuzeigen als den, den man bewegt hat.
  selected.value = null
}

function deleteSelected(): void {
  if (selected.value) {
    document.value = removeBlock(document.value, selected.value)
    selected.value = null
  }
}

function changeBinding(control: string, binding: Binding<unknown>): void {
  if (selected.value) {
    document.value = setBlockBinding(document.value, selected.value, control, binding)
  }
}

function clearBinding(control: string): void {
  if (selected.value) {
    document.value = clearBlockBinding(document.value, selected.value, control)
  }
}

/**
 * Autosave, entprellt. Nach §7.2 erzeugt er keine Version — nur Veröffentlichen tut das.
 *
 * Die Antwort trägt den neuen Stempel; ohne ihn ließe sich genau einmal speichern. Ein Konflikt
 * hält den Editor an, statt weiterzuschreiben: Wer ihn sieht, entscheidet selbst, ob er neu
 * lädt. Automatisch nachzuladen würde die eigene Arbeit überschreiben, automatisch weiter zu
 * speichern die des anderen.
 */
let autosaveTimer: ReturnType<typeof setTimeout> | undefined
watch(document, () => {
  // Der Referenzvergleich ist der Grund, aus dem die Dokumentänderungen unveränderlich sind.
  // Ohne ihn zählte auch das Laden als Änderung, und der Editor schriebe direkt nach dem
  // Öffnen dasselbe zurück — mit neuem Änderungsstempel. Zwei Leute, die eine Seite nur
  // ANSEHEN, gäben sich damit gegenseitig einen Konflikt.
  if (!layout.value || conflict.value || document.value === savedDocument.value) {
    return
  }

  clearTimeout(autosaveTimer)
  autosaveTimer = setTimeout(() => void save(), AUTOSAVE_DELAY_MS)
})

async function save(): Promise<void> {
  const current = layout.value
  if (!current || changedAtUtc.value === null) {
    return
  }

  saving.value = true
  // Festhalten, WAS gerade gesendet wird: Eine Änderung während des Speicherns darf nicht
  // mitgezählt werden, sonst gilt sie als gespeichert und der nächste Autosave bliebe aus.
  const sending = document.value
  try {
    const response = await fetch(draftUrl(current.layoutKey), {
      method: 'PUT',
      credentials: 'same-origin',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        document: sending,
        expectedChangedAtUtc: changedAtUtc.value,
      }),
    })

    if (response.status === 409) {
      conflict.value = true
      return
    }

    if (!response.ok) {
      error.value = 'Die Änderung konnte nicht gespeichert werden.'
      return
    }

    const saved = await response.json()
    changedAtUtc.value = saved.changedAtUtc
    savedDocument.value = sending
  } catch {
    error.value = 'Die Änderung konnte nicht gespeichert werden.'
  } finally {
    saving.value = false
  }
}

/**
 * Veröffentlichen und Verwerfen — die beiden Übergänge, die über den Unterschied zwischen dem
 * entscheiden, was jemand gebaut hat, und dem, was Besucher sehen (§7.2).
 *
 * Vor beiden wird der ausstehende Autosave abgewartet. Sonst veröffentlichte man einen Stand,
 * der die letzte Änderung noch nicht enthält — und niemand sähe, dass etwas fehlt.
 */
async function transition(action: 'publish' | 'discard'): Promise<void> {
  const current = layout.value
  if (!current || publishing.value) {
    return
  }

  clearTimeout(autosaveTimer)
  await save()
  if (conflict.value) {
    return
  }

  publishing.value = true
  error.value = null
  try {
    const response = await fetch(
      `/api/ext/admin/composer/layouts/${encodeURIComponent(current.layoutKey)}/${action}`,
      { method: 'POST', credentials: 'same-origin' },
    )
    if (!response.ok) {
      error.value = action === 'publish'
        ? 'Die Veröffentlichung ist fehlgeschlagen.'
        : 'Der Entwurf konnte nicht verworfen werden.'
      return
    }

    published.value = action === 'publish'
    // Neu laden: Verwerfen ersetzt den Entwurf durch den veröffentlichten Stand, und
    // Veröffentlichen beginnt einen neuen mit neuer Nummer und neuem Stempel. Beides weiter
    // gegen den alten Stempel zu speichern gäbe einen Konflikt gegen sich selbst.
    await load()
  } catch {
    error.value = 'Die Aktion konnte nicht ausgeführt werden.'
  } finally {
    publishing.value = false
  }
}

/** Ob seit dem letzten Laden veröffentlicht wurde — für die Rückmeldung, sonst nichts. */
const published = ref(false)

function draftUrl(key: string): string {
  return `/api/ext/admin/composer/layouts/${encodeURIComponent(key)}/draft`
}
</script>

<template>
  <section class="composer">
    <header class="composer__header">
      <h1>Flächen gestalten</h1>
      <form class="composer__load" @submit.prevent="load">
        <label for="composer-layout-key">Layout</label>
        <!--
          Auswahl, wo es etwas zu wählen gibt; Textfeld, wo die Liste nicht geladen werden
          konnte. Wer den Schlüssel kennt, soll dann weiterarbeiten können.
        -->
        <select v-if="layouts.length > 0" id="composer-layout-key" v-model="layoutKey">
          <option value="">Layout wählen …</option>
          <option v-for="entry in layouts" :key="entry.layoutKey" :value="entry.layoutKey">
            {{ entry.name }}{{ entry.surfaceKey ? ` — ${entry.surfaceKey}` : ' — ohne Fläche' }}{{
              entry.hasPublishedVersion ? '' : ' (nicht veröffentlicht)'
            }}
          </option>
        </select>
        <input v-else id="composer-layout-key" v-model="layoutKey" type="text" placeholder="portal" />
        <button type="submit" :disabled="loading || !layoutKey">Laden</button>
      </form>
    </header>

    <p v-if="error" class="composer__error" role="alert">{{ error }}</p>

    <!--
      Ein Konflikt hält an, statt zu überschreiben. Automatisch neu zu laden verlöre die eigene
      Arbeit, automatisch weiterzuspeichern die des anderen — beides ist eine Entscheidung, die
      der Editor nicht treffen darf.
    -->
    <p v-if="conflict" class="composer__conflict" role="alert">
      Jemand anderes hat diesen Entwurf inzwischen geändert. Weitere Änderungen werden nicht mehr
      gespeichert. Laden Sie neu, um mit dem aktuellen Stand weiterzuarbeiten.
    </p>

    <!--
      Ein Layout ohne Fläche ist erlaubt — es darf gebaut werden, bevor jemand entscheidet, wo
      es hingeht. Nur muss der Editor es sagen: Er lädt dann die Blöcke der Standardfläche, und
      wer das nicht weiß, wundert sich später, warum ein Block auf einmal fehlt.
    -->
    <p v-if="layout && !layout.surfaceKey" class="composer__hint" role="status">
      Dieses Layout ist noch keiner Fläche zugeordnet. Angeboten werden die Blöcke der
      Standardfläche.
    </p>

    <p v-if="bundleFailures.length > 0" class="composer__hint" role="status">
      Blöcke aus {{ bundleFailures.map((failure) => failure.pluginId).join(', ') }} konnten nicht
      geladen werden. Sie erscheinen im Canvas als Platzhalter.
    </p>

    <template v-if="layout">
      <div class="composer__modes">
        <!--
          Der Umschalter aus §7.6: Er ändert, wie ein Block auf Zeigereingaben reagiert, nicht
          was gerade ausgewählt ist. Wer einen Akkordeon-Block aufklappen will, um zu sehen, was
          darin steht, soll dafür nicht die Auswahl verlieren.
        -->
        <label>
          <input v-model="editing" type="checkbox" />
          Blöcke auswählen statt bedienen
        </label>
        <span class="composer__status">{{ saving ? 'Wird gespeichert …' : '' }}</span>

        <!--
          Der Entwurf ist der Normalzustand; Veröffentlichen ist die Entscheidung. Deshalb
          steht der Zustand links und die Aktion rechts, und „Verwerfen" ist zurückhaltender
          gestaltet als „Veröffentlichen" — es wirft Arbeit weg.
        -->
        <div class="composer__transitions">
          <span class="composer__status">{{ published ? 'Veröffentlicht' : 'Entwurf' }}</span>
          <button type="button" :disabled="publishing || conflict" @click="transition('discard')">
            Verwerfen
          </button>
          <button
            type="button"
            class="composer__publish"
            :disabled="publishing || conflict"
            @click="transition('publish')"
          >
            Veröffentlichen
          </button>
        </div>
      </div>

      <!--
        Die Layouts kommen aus dem Theme, nicht aus dem Editor (§7.1). Angeboten wird
        ausschließlich, was das Theme stylen kann — so bleibt die Token-Achse die
        Design-Autorität, und es steht kein Layout-Name im Core.
      -->
      <div class="composer__sections">
        <label for="composer-new-section">Sektion hinzufügen</label>
        <select
          id="composer-new-section"
          :disabled="!hasLayouts"
          @change="appendSection(($event.target as HTMLSelectElement).value)"
        >
          <option value="">Layout wählen …</option>
          <option v-for="layout in sectionLayouts" :key="layout.layoutKey" :value="layout.layoutKey">
            {{ layout.label }}
          </option>
        </select>
        <!--
          Kein Layout heißt nicht „Editor kaputt", sondern „dieses Theme bringt keine mit". Ohne
          diesen Satz sucht jemand den Fehler im Editor.
        -->
        <!--
          Der Server liefert immer mindestens die Basis-Layouts. Leer heißt hier also: Das
          Theme war nicht erreichbar — nicht „dieses Theme kann keine".
        -->
        <p v-if="!hasLayouts" class="composer__status">
          Die Sektionslayouts konnten nicht geladen werden.
        </p>
      </div>

      <p v-if="strandedSections.length > 0" class="composer__hint" role="status">
        {{ strandedSections.length }} Sektion(en) benutzen ein Layout, das dieses Theme nicht
        kennt ({{ [...new Set(strandedSections.map((entry) => entry.section.layout))].join(', ') }}).
        Sie werden einspaltig ausgeliefert; der Inhalt bleibt vollständig.
      </p>

      <ul v-if="document.sections.length > 0" class="composer__section-list">
        <li v-for="(section, index) in document.sections" :key="index">
          <label :for="`composer-section-${index}`">Sektion {{ index + 1 }}</label>
          <select
            :id="`composer-section-${index}`"
            :value="section.layout"
            @change="changeSectionLayout(index, ($event.target as HTMLSelectElement).value)"
          >
            <!--
              Das aktuelle Layout steht auch dann in der Liste, wenn das Theme es nicht kennt.
              Sonst zeigte die Auswahl etwas anderes an, als im Dokument steht, und der nächste
              Klick irgendwohin änderte still das Layout.
            -->
            <option v-if="!sectionLayouts.some((l) => l.layoutKey === section.layout)"
                    :value="section.layout">
              {{ section.layout }} (unbekannt)
            </option>
            <option v-for="layout in sectionLayouts" :key="layout.layoutKey" :value="layout.layoutKey">
              {{ layout.label }}
            </option>
          </select>
        </li>
      </ul>

      <div
        class="composer__workspace"
        @dragstart="dragging = true"
        @dragend="dragging = false"
        @drop="dragging = false"
      >
        <BlockPalette :blocks="availableBlocks" :categories="blockCategories" />

        <ComposerCanvas
          :document="document"
          :surface-css="canvasCss"
          :tokens="tokens"
          :layouts="sectionLayouts"
          :selected="selected"
          :editing="editing"
          :dragging="dragging"
          @select="selected = $event"
          @drop-block="handleDrop"
          @drag-block="selected = $event"
        />

        <BlockInspector
          v-if="selectedBlock"
          :block="selectedBlock"
          :definition="selectedDefinition"
          :token-roles="tokenRoles"
          @change="changeBinding"
          @clear="clearBinding"
          @remove="deleteSelected"
        />
        <p v-else class="composer__status">Wählen Sie einen Block aus, um ihn einzustellen.</p>
      </div>
    </template>
  </section>
</template>
