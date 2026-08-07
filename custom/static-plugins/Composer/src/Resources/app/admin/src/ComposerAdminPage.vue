<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  loadSurfaceBundles,
  surfaceBaseTokens,
  type Binding,
  type BlockDefinition,
  type PluginLoadResult,
} from '@callora/surface'
import ComposerCanvas from './ComposerCanvas.vue'
import BlockInspector from './BlockInspector.vue'
import { fetchSurfaceStyles, fetchThemeTokens } from './preview-assets'
import { collectTokenRoles } from './token-roles'
import {
  blockAt,
  clearBlockBinding,
  emptyDocument,
  readDocument,
  setBlockBinding,
  type BlockAddress,
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
const layout = ref<LoadedLayout | null>(null)
const document = ref<LayoutDocument>(emptyDocument())
const changedAtUtc = ref<string | null>(null)
const surfaceCss = ref('')
const tokens = ref<Record<string, string>>({})
const bundleFailures = ref<PluginLoadResult[]>([])
const selected = ref<BlockAddress | null>(null)
const editing = ref(true)
const error = ref<string | null>(null)
const conflict = ref(false)
const saving = ref(false)
const loading = ref(false)

/**
 * Die Token-Rollen, aus denen ein Erscheinungs-Control wählen darf — gelesen aus genau dem CSS,
 * das im Canvas gilt. Nicht aus einer gepflegten Liste: Die liefe auseinander, sobald jemand ein
 * Token hinzufügt, und das Panel böte dann Rollen an, die es nicht gibt (oder verschwiege
 * welche, die es gibt).
 */
const canvasCss = computed(() => `${surfaceBaseTokens}\n${surfaceCss.value}`)
const tokenRoles = computed(() => collectTokenRoles(canvasCss.value))

const selectedBlock = computed(() => blockAt(document.value, selected.value))

const selectedDefinition = computed<BlockDefinition | undefined>(() => {
  const blockId = selectedBlock.value?.blockId
  if (!blockId) {
    return undefined
  }

  const registry = (globalThis as { calloraSurface?: { blocks?: { blocks: BlockDefinition[] } } })
    .calloraSurface?.blocks
  return registry?.blocks.find((block) => block.id === blockId)
})

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
    document.value = readDocument(draft.document)
    changedAtUtc.value = draft.changedAtUtc ?? null

    await loadPreview(layout.value)
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

  const [css, themeValues] = await Promise.all([
    fetchSurfaceStyles(bundles.styles),
    fetchThemeTokens(loaded.workspaceKey),
  ])
  surfaceCss.value = css
  tokens.value = themeValues
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
  if (!layout.value || conflict.value) {
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
  try {
    const response = await fetch(draftUrl(current.layoutKey), {
      method: 'PUT',
      credentials: 'same-origin',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        document: document.value,
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
  } catch {
    error.value = 'Die Änderung konnte nicht gespeichert werden.'
  } finally {
    saving.value = false
  }
}

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
        <input id="composer-layout-key" v-model="layoutKey" type="text" placeholder="portal" />
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
      </div>

      <div class="composer__workspace">
        <ComposerCanvas
          :document="document"
          :surface-css="canvasCss"
          :tokens="tokens"
          :selected="selected"
          :editing="editing"
          @select="selected = $event"
        />

        <BlockInspector
          v-if="selectedBlock"
          :block="selectedBlock"
          :definition="selectedDefinition"
          :token-roles="tokenRoles"
          @change="changeBinding"
          @clear="clearBinding"
        />
        <p v-else class="composer__status">Wählen Sie einen Block aus, um ihn einzustellen.</p>
      </div>
    </template>
  </section>
</template>
