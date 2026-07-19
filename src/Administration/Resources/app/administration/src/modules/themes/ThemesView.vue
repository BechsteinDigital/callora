<template>
  <section class="themes">
    <header class="head">
      <h1>Themes</h1>
      <div class="head-actions">
        <ExtensionSlot name="themes.toolbar" />
      </div>
    </header>

    <p class="intro">
      Weist einem Workspace ein Theme-Bundle zu (Token-Achse — Branding/Farben, ADR-014 §10). Die Auswahl
      gilt für die Surfaces des Workspaces und lässt sich pro Surface weiter überschreiben.
    </p>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Lädt…</p>
    <p v-else-if="!activeWorkspace" class="empty">Kein Workspace ausgewählt.</p>

    <div v-else class="body">
      <div class="current">
        <h2>Aktuelles Theme</h2>
        <div v-if="assignment && assignment.themePluginId" class="assigned">
          <span class="mono">{{ assignment.themePluginId }}@{{ assignment.themeVersion }}</span>
          <span v-if="assignment.assignedBy" class="sub">zugewiesen von {{ assignment.assignedBy }}</span>
          <button
            v-if="canManage"
            type="button"
            class="link-danger"
            :disabled="busy"
            @click="clear"
          >
            Entfernen
          </button>
        </div>
        <p v-else class="empty">Kein Theme zugewiesen — es gilt der Distributions-Default.</p>
      </div>

      <form v-if="canManage" class="assign" @submit.prevent="assign">
        <label class="pick">Theme
          <select v-model.number="selectedIndex" name="themeDefinition" class="select">
            <option v-for="(d, i) in definitions" :key="d.templateKey" :value="i">
              {{ d.displayName }} ({{ d.pluginId }}@{{ d.version }})
            </option>
          </select>
        </label>
        <BaseButton type="submit" :disabled="busy || !definitions.length">
          {{ busy ? 'Weist zu…' : 'Zuweisen' }}
        </BaseButton>
      </form>

      <p v-if="canManage && !definitions.length" class="hint">
        Keine aktiven Workspace-Themes registriert. Ein Theme-Plugin muss zuerst eine Definition
        (surface „workspace") beitragen.
      </p>

      <ExtensionSlot name="themes.after-assignment" :ctx="{ workspaceKey: activeWorkspace, assignment }" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { themesApi, type ThemeAssignment, type ThemeDefinition } from './themesApi'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'extension.update'))

const { activeWorkspace, ensure: ensureWorkspace } = useWorkspaceContext()

// Resolve the themes service through the override registry: a plugin may replace it.
const api = useService('themesApi', themesApi)

const definitions = ref<ThemeDefinition[]>([])
const assignment = ref<ThemeAssignment | null>(null)
const selectedIndex = ref(0)
const loading = ref(true)
const error = ref<string | null>(null)
const busy = ref(false)

async function load(): Promise<void> {
  if (!activeWorkspace.value) {
    definitions.value = []
    assignment.value = null
    loading.value = false
    return
  }
  loading.value = true
  error.value = null
  try {
    const [defs, current] = await Promise.all([
      api.listDefinitions(),
      api.getAssignment(activeWorkspace.value),
    ])
    definitions.value = defs
    assignment.value = current
    selectedIndex.value = 0
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function assign(): Promise<void> {
  const definition = definitions.value[selectedIndex.value]
  if (!definition || busy.value) {
    return
  }
  error.value = null
  const before = await runHook('themes.before-assign', {
    workspaceKey: activeWorkspace.value,
    themePluginId: definition.pluginId,
    themeVersion: definition.version,
  })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Zuweisung abgebrochen.'
    return
  }
  busy.value = true
  try {
    await api.assign(activeWorkspace.value, definition.pluginId, definition.version)
    await runHook('themes.after-assign', { workspaceKey: activeWorkspace.value, themePluginId: definition.pluginId })
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

async function clear(): Promise<void> {
  if (busy.value) {
    return
  }
  if (!window.confirm('Theme-Zuweisung dieses Workspaces entfernen?')) {
    return
  }
  error.value = null
  const before = await runHook('themes.before-clear', { workspaceKey: activeWorkspace.value })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Entfernen abgebrochen.'
    return
  }
  busy.value = true
  try {
    await api.clearAssignment(activeWorkspace.value)
    await runHook('themes.after-clear', { workspaceKey: activeWorkspace.value })
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

watch(activeWorkspace, load, { immediate: true })

onMounted(() => {
  void ensureWorkspace().catch((e) => {
    error.value = (e as Error).message
    loading.value = false
  })
})
</script>

<style scoped lang="scss">
.themes {
  padding: calc(var(--cal-space) * 3);
}

.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--cal-space);
}

.head-actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
}

.intro {
  color: var(--cal-color-muted);
  margin-bottom: calc(var(--cal-space) * 2);
  max-width: 640px;
}

.current {
  margin-bottom: calc(var(--cal-space) * 2);
}

.current h2 {
  font-size: 1.1em;
  margin-bottom: var(--cal-space);
}

.assigned {
  display: flex;
  align-items: center;
  gap: calc(var(--cal-space) * 1.5);
}

.mono {
  font-family: var(--cal-font-mono, monospace);
}

.sub {
  color: var(--cal-color-muted);
  font-size: 0.9em;
}

.assign {
  display: flex;
  gap: var(--cal-space);
  align-items: flex-end;
  flex-wrap: wrap;
}

.pick {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
  max-width: 360px;
}

.select {
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.hint {
  color: var(--cal-color-muted);
  margin-top: var(--cal-space);
}

.link-danger {
  background: none;
  border: 0;
  color: var(--cal-color-danger);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link-danger:disabled {
  opacity: 0.5;
  cursor: default;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
