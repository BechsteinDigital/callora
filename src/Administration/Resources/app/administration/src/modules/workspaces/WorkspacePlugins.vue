<template>
  <section class="plugins">
    <div class="heading">
      <div>
        <h2>Plugins</h2>
        <p>Plugins für diesen Workspace freischalten und aktivieren.</p>
      </div>
    </div>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="loading">Lädt…</p>

    <div v-else class="plugin-list">
      <article v-for="plugin in plugins" :key="plugin.pluginId" class="plugin-card">
        <div class="plugin-copy">
          <strong>{{ plugin.displayName }}</strong>
          <code>{{ plugin.pluginId }}</code>
          <div class="states">
            <span class="badge" :class="plugin.isGloballyActive ? 'positive' : 'neutral'">
              {{ plugin.isGloballyActive ? 'Global aktiv' : 'Global inaktiv' }}
            </span>
            <span class="badge" :class="plugin.isAssigned ? 'positive' : 'neutral'">
              {{ plugin.isAssigned ? 'Zugewiesen' : 'Nicht zugewiesen' }}
            </span>
          </div>
          <small v-if="!plugin.isGloballyActive && !plugin.isAssigned">
            Zuerst global aktivieren.
          </small>
        </div>

        <BaseButton
          v-if="canManage"
          type="button"
          :data-testid="`plugin-assignment-${plugin.pluginId}`"
          :disabled="busyPluginId === plugin.pluginId || (!plugin.isGloballyActive && !plugin.isAssigned)"
          @click="setAssignment(plugin)"
        >
          {{ plugin.isAssigned ? 'Entfernen' : 'Zuweisen' }}
        </BaseButton>
      </article>

      <p v-if="plugins.length === 0" class="empty">Keine installierten Plugins vorhanden.</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import BaseButton from '@/core/ui/BaseButton.vue'
import { useService } from '@/core/extensions/services'
import {
  workspacesApi,
  type WorkspacePluginAssignment,
} from './workspacesApi'

const props = defineProps<{ workspaceKey: string; canManage: boolean }>()
const api = useService('workspacesApi', workspacesApi)
const plugins = ref<WorkspacePluginAssignment[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyPluginId = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    plugins.value = await api.listPlugins(props.workspaceKey)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function setAssignment(plugin: WorkspacePluginAssignment): Promise<void> {
  if (busyPluginId.value) {
    return
  }
  busyPluginId.value = plugin.pluginId
  error.value = null
  try {
    const updated = await api.setPluginAssignment(
      props.workspaceKey,
      plugin.pluginId,
      !plugin.isAssigned,
    )
    const index = plugins.value.findIndex((item) => item.pluginId === plugin.pluginId)
    if (index >= 0) {
      plugins.value[index] = updated
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyPluginId.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.plugins {
  margin-top: calc(var(--cal-space) * 3);
}

.heading h2 {
  margin: 0;
  font-size: 1.1em;
}

.heading p {
  margin: 4px 0 var(--cal-space);
  color: var(--cal-color-muted);
}

.plugin-list {
  display: grid;
  gap: var(--cal-space);
}

.plugin-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: calc(var(--cal-space) * 2);
  padding: calc(var(--cal-space) * 1.5);
  border: 1px solid var(--cal-color-surface);
  border-radius: var(--cal-radius);
}

.plugin-copy {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.plugin-copy code,
.plugin-copy small {
  color: var(--cal-color-muted);
}

.states {
  display: flex;
  flex-wrap: wrap;
  gap: calc(var(--cal-space) * 0.75);
}

.badge {
  padding: 1px calc(var(--cal-space) * 0.75);
  border: 1px solid currentColor;
  border-radius: var(--cal-radius);
  font-size: 0.75em;
}

.positive {
  color: var(--cal-color-accent);
}

.neutral {
  color: var(--cal-color-muted);
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
