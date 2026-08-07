<script setup lang="ts">
import { ref } from 'vue'
import ComposerCanvas from './ComposerCanvas.vue'

/**
 * Die Composer-Seite in der Admin-Shell.
 *
 * Sie holt den Entwurf über die Admin-API des Plugins — dieselbe Route, die eine
 * Operator-Berechtigung verlangt. Der öffentliche Renderpfad kommt hier nicht hin: Er ruft
 * GetPublishedAsync auf einem anderen Vertrag, und es gibt keinen Parameter, der das eine ins
 * andere verwandelt.
 */
const layoutKey = ref('')
const document = ref<{ sections?: unknown[] }>({ sections: [] })
const changedAtUtc = ref<string | null>(null)
const error = ref<string | null>(null)
const loading = ref(false)

async function load(): Promise<void> {
  if (!layoutKey.value) {
    return
  }

  loading.value = true
  error.value = null
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
    document.value = draft.document ?? { sections: [] }
    changedAtUtc.value = draft.changedAtUtc ?? null
  } catch {
    error.value = 'Der Entwurf konnte nicht geladen werden.'
  } finally {
    loading.value = false
  }
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

    <ComposerCanvas :document="document" />
  </section>
</template>
