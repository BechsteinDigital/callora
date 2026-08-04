<template>
  <CalPage narrow>
    <CalPageHeader title="Themes" description="Branding und Farbachse der Surfaces eines Workspaces.">
      <template #actions>
        <ExtensionSlot name="themes.toolbar" />
      </template>
    </CalPageHeader>

    <CalAlert v-if="error" class="themes__message" tone="danger">{{ error }}</CalAlert>

    <CalCard v-if="loading">
      <CalSkeleton height="80px" />
    </CalCard>

    <CalCard v-else-if="!activeWorkspace">
      <CalEmptyState
        :icon="Boxes"
        title="Kein Workspace ausgewählt."
        description="Wählen Sie oben rechts einen Workspace, um dessen Theme zu verwalten."
      />
    </CalCard>

    <template v-else>
      <CalCard
        title="Aktuelles Theme"
        description="Gilt für alle Surfaces des Workspaces und lässt sich pro Surface weiter überschreiben."
      >
        <template v-if="assignment && assignment.themePluginId" #actions>
          <CalButton v-if="canManage" variant="danger-ghost" size="sm" :disabled="busy" @click="clear">
            Entfernen
          </CalButton>
        </template>

        <div v-if="assignment && assignment.themePluginId" class="themes__assigned">
          <CalBadge tone="accent" variant="outline">
            {{ assignment.themePluginId }}@{{ assignment.themeVersion }}
          </CalBadge>
          <span v-if="assignment.assignedBy" class="themes__by">zugewiesen von {{ assignment.assignedBy }}</span>
        </div>
        <p v-else class="themes__none">Kein Theme zugewiesen — es gilt der Distributions-Default.</p>
      </CalCard>

      <CalCard v-if="canManage" class="themes__assign" title="Theme zuweisen">
        <form v-if="definitions.length" class="themes__form" @submit.prevent="assign">
          <CalField v-slot="{ id }" label="Verfügbare Themes">
            <CalSelect :id="id" v-model="selectedValue" name="themeDefinition">
              <option v-for="(d, i) in definitions" :key="`${d.templateKey}:${d.pluginId}:${d.version}`" :value="i">
                {{ d.displayName }} ({{ d.pluginId }}@{{ d.version }})
              </option>
            </CalSelect>
          </CalField>
          <CalButton type="submit" variant="primary" :loading="busy">Zuweisen</CalButton>
        </form>
        <CalEmptyState
          v-else
          compact
          :icon="Palette"
          title="Keine aktiven Workspace-Themes registriert."
          description="Ein Theme-Plugin muss zuerst eine Definition für die Surface „workspace“ beitragen."
        />
      </CalCard>

      <ThemeSettings
        v-if="assignment && assignment.themePluginId"
        :key="`${activeWorkspace}:${assignment.themePluginId}:${assignment.themeVersion}`"
        class="themes__settings"
        :workspace-key="activeWorkspace"
        :can-manage="canManage"
      />

      <ExtensionSlot name="themes.after-assignment" :ctx="{ workspaceKey: activeWorkspace, assignment }" />
    </template>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Boxes, Palette } from 'lucide-vue-next'
import { themesApi, type ThemeAssignment, type ThemeDefinition } from './themesApi'
import ThemeSettings from './ThemeSettings.vue'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

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

// CalSelect models a string; the picker addresses definitions by index.
const selectedValue = computed({
  get: () => String(selectedIndex.value),
  set: (value: string) => {
    selectedIndex.value = Number(value)
  },
})

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
    const [defs, current] = await Promise.all([api.listDefinitions(), api.getAssignment(activeWorkspace.value)])
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
    toast.success(`Theme „${definition.displayName}“ zugewiesen.`)
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
  const confirmed = await confirm({
    title: 'Theme-Zuweisung entfernen?',
    description: 'Für die Surfaces dieses Workspaces gilt danach wieder der Distributions-Default.',
    confirmLabel: 'Entfernen',
    tone: 'danger',
  })
  if (!confirmed) {
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
    toast.success('Theme-Zuweisung entfernt.')
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
.themes__message {
  margin-bottom: var(--cal-space-4);
}

.themes__assigned {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.themes__by,
.themes__none {
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
}

.themes__assign,
.themes__settings {
  margin-top: var(--cal-space-4);
}

.themes__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.themes__form > :deep(.cal-field) {
  flex: 1;
  min-width: 240px;
}
</style>
