<template>
  <div class="ws-plugins">
    <CalAlert v-if="error" class="ws-plugins__error" tone="danger">{{ error }}</CalAlert>

    <div v-if="loading" class="ws-plugins__list">
      <CalSkeleton v-for="n in 3" :key="n" height="64px" />
    </div>

    <CalEmptyState
      v-else-if="!plugins.length"
      :icon="Puzzle"
      title="Keine installierten Plugins vorhanden."
      description="Installieren Sie zuerst ein Plugin unter „Plugins“."
    />

    <div v-else class="ws-plugins__list">
      <article v-for="plugin in plugins" :key="plugin.pluginId" class="ws-plugins__item">
        <div class="ws-plugins__copy">
          <div class="ws-plugins__title">
            <strong>{{ plugin.displayName }}</strong>
            <code>{{ plugin.pluginId }}</code>
          </div>
          <div class="ws-plugins__states">
            <CalBadge :tone="plugin.isGloballyActive ? 'success' : 'neutral'" dot>
              {{ plugin.isGloballyActive ? 'Global aktiv' : 'Global inaktiv' }}
            </CalBadge>
            <CalBadge :tone="plugin.isAssigned ? 'accent' : 'neutral'" variant="outline">
              {{ plugin.isAssigned ? 'Zugewiesen' : 'Nicht zugewiesen' }}
            </CalBadge>
          </div>
          <p v-if="!plugin.isGloballyActive && !plugin.isAssigned" class="ws-plugins__hint">
            Zuerst global aktivieren.
          </p>
        </div>

        <CalButton
          v-if="canManage"
          :variant="plugin.isAssigned ? 'danger-ghost' : 'secondary'"
          :data-testid="`plugin-assignment-${plugin.pluginId}`"
          :disabled="busyPluginId === plugin.pluginId || (!plugin.isGloballyActive && !plugin.isAssigned)"
          @click="setAssignment(plugin)"
        >
          {{ plugin.isAssigned ? 'Entfernen' : 'Zuweisen' }}
        </CalButton>
      </article>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Puzzle } from 'lucide-vue-next'
import { useService } from '@/core/extensions/services'
import { workspacesApi, type WorkspacePluginAssignment } from './workspacesApi'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'
import { toast } from '@/core/feedback/toasts'

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
    const updated = await api.setPluginAssignment(props.workspaceKey, plugin.pluginId, !plugin.isAssigned)
    const index = plugins.value.findIndex((item) => item.pluginId === plugin.pluginId)
    if (index >= 0) {
      plugins.value[index] = updated
    }
    toast.success(
      updated.isAssigned
        ? `„${plugin.displayName}“ diesem Workspace zugewiesen.`
        : `„${plugin.displayName}“ aus diesem Workspace entfernt.`,
    )
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyPluginId.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.ws-plugins__error {
  margin-bottom: var(--cal-space-4);
}

.ws-plugins__list {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
}

.ws-plugins__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cal-space-4);
  padding: var(--cal-space-4);
  background: var(--cal-surface);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-lg);
}

.ws-plugins__copy {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
  min-width: 0;
}

.ws-plugins__title {
  display: flex;
  align-items: baseline;
  gap: var(--cal-space-2);
  flex-wrap: wrap;
}

.ws-plugins__title code {
  color: var(--cal-text-muted);
  font-size: var(--cal-text-sm);
}

.ws-plugins__states {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  flex-wrap: wrap;
}

.ws-plugins__hint {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}
</style>
