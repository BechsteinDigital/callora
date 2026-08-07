<script setup lang="ts">
import { ref } from 'vue'
import { loadSurfaceBundles, type PluginLoadResult } from '@callora/surface'
import ComposerCanvas from './ComposerCanvas.vue'
import { fetchSurfaceStyles, fetchThemeTokens } from './preview-assets'

/**
 * Die Composer-Seite in der Admin-Shell.
 *
 * Sie holt den Entwurf über die Admin-API des Plugins — dieselbe Route, die eine
 * Operator-Berechtigung verlangt. Der öffentliche Renderpfad kommt hier nicht hin: Er ruft
 * GetPublishedAsync auf einem anderen Vertrag, und es gibt keinen Parameter, der das eine ins
 * andere verwandelt.
 *
 * Danach lädt sie die Block-Bundles der Fläche, für die das Layout gedacht ist. Das ist der
 * Schritt, ohne den der Canvas nur Platzhalter zeigen könnte: Die Blöcke sind Vue-Komponenten
 * aus Plugin-Bundles, und im Admin lädt die Shell nur Bundles der `admin`-Fläche.
 *
 * Der Canvas erscheint erst, wenn ein Layout geladen ist. Das ist keine Kosmetik: Die Registry
 * entsteht mit den Schlüsseln des Layouts, und sie entsteht genau einmal. Wäre sie vorher da,
 * hinge ihr Kontextkanal an „default" statt an der Fläche, die hier gestaltet wird.
 */
interface LoadedLayout {
  layoutKey: string
  workspaceKey: string
  surfaceKey: string | null
  document: { sections?: unknown[] }
  changedAtUtc: string | null
}

const layoutKey = ref('')
const layout = ref<LoadedLayout | null>(null)
const surfaceCss = ref('')
const tokens = ref<Record<string, string>>({})
const bundleFailures = ref<PluginLoadResult[]>([])
const error = ref<string | null>(null)
const loading = ref(false)

async function load(): Promise<void> {
  if (!layoutKey.value) {
    return
  }

  loading.value = true
  error.value = null
  bundleFailures.value = []
  try {
    const response = await fetch(
      `/api/ext/admin/composer/layouts/${encodeURIComponent(layoutKey.value)}/draft`,
      { credentials: 'same-origin' },
    )

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
      document: draft.document ?? { sections: [] },
      changedAtUtc: draft.changedAtUtc ?? null,
    }

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

    <ComposerCanvas
      v-if="layout"
      :document="layout.document"
      :surface-css="surfaceCss"
      :tokens="tokens"
    />
  </section>
</template>
