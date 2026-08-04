<template>
  <CalDialog
    :open="open"
    size="md"
    :title="`Design der Surface „${surfaceKey}“`"
    description="Eine Surface kann das Theme ihres Workspaces mit eigenen Werten nutzen — oder ein eigenes Theme bekommen."
    @update:open="$emit('update:open', $event)"
  >
    <CalAlert v-if="error" class="surface-theme__message" tone="danger">{{ error }}</CalAlert>

    <div v-if="loading" class="surface-theme__skeletons">
      <CalSkeleton v-for="n in 4" :key="n" height="36px" />
    </div>

    <template v-else>
      <section class="surface-theme__block">
        <div class="surface-theme__block-head">
          <h3 class="surface-theme__block-title">Theme</h3>
          <CalBadge :tone="assignment?.inheritedFromWorkspace ? 'neutral' : 'accent'" variant="outline">
            {{ assignment?.inheritedFromWorkspace ? 'vom Workspace geerbt' : 'eigenes Theme' }}
          </CalBadge>
        </div>

        <CalEmptyState
          v-if="!assignment?.themePluginId"
          compact
          :icon="Palette"
          title="Kein Theme aktiv."
          description="Weisen Sie dem Workspace ein Theme zu oder wählen Sie hier eines für diese Surface."
        />

        <div class="surface-theme__assign">
          <CalField v-slot="{ id }" label="Verwendetes Theme">
            <CalSelect :id="id" v-model="selectedTheme" name="surfaceTheme" :disabled="!canManage">
              <option value="">— Theme des Workspaces verwenden —</option>
              <option v-for="d in definitions" :key="`${d.pluginId}@${d.version}`" :value="`${d.pluginId}@${d.version}`">
                {{ d.displayName }} ({{ d.pluginId }}@{{ d.version }})
              </option>
            </CalSelect>
          </CalField>
          <CalButton
            v-if="canManage"
            variant="secondary"
            :loading="switching"
            :disabled="selectedTheme === currentThemeValue"
            @click="applyTheme"
          >
            Theme übernehmen
          </CalButton>
        </div>

        <CalAlert v-if="assignment && !assignment.inheritedFromWorkspace && !inheritsWorkspaceValues" tone="info">
          Diese Surface nutzt ein anderes Theme als ihr Workspace — dessen Werte werden deshalb nicht vererbt.
        </CalAlert>
      </section>

      <section v-if="settings?.hasAssignedTheme" class="surface-theme__block">
        <div class="surface-theme__block-head">
          <h3 class="surface-theme__block-title">Werte</h3>
          <span class="surface-theme__hint">Leer = geerbter Wert</span>
        </div>

        <CalEmptyState v-if="!activeFields.length" compact title="Dieses Theme stellt keine Einstellungen bereit." />

        <div v-else class="surface-theme__fields">
          <CalField
            v-for="field in activeFields"
            :key="field.settingKey"
            v-slot="{ id }"
            :label="field.label"
            :hint="field.groupName || undefined"
            :description="describeInheritance(field.settingKey)"
          >
            <CalInput
              :id="id"
              v-model="inputs[field.settingKey]"
              :name="`surface-setting-${field.settingKey}`"
              :placeholder="inheritedPlaceholder(field)"
              :disabled="!canManage"
            >
              <template v-if="isOverridden(field.settingKey)" #suffix>
                <button type="button" class="surface-theme__reset" title="Auf geerbten Wert zurücksetzen" @click="reset(field.settingKey)">
                  <CalIcon :icon="RotateCcw" size="sm" />
                </button>
              </template>
            </CalInput>
          </CalField>
        </div>
      </section>
    </template>

    <template #footer>
      <CalButton variant="ghost" @click="$emit('update:open', false)">Schließen</CalButton>
      <CalButton
        v-if="canManage && settings?.hasAssignedTheme && activeFields.length"
        variant="primary"
        :loading="saving"
        @click="save"
      >
        Werte speichern
      </CalButton>
    </template>
  </CalDialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { Palette, RotateCcw } from 'lucide-vue-next'
import {
  themesApi,
  type SurfaceThemeAssignment,
  type SurfaceThemeSettings,
  type ThemeDefinition,
  type ThemeSettingDefinition,
} from './themesApi'
import { coerceInputToJsonValue, displayJsonValue } from './themesValues'
import { useService } from '@/core/extensions/services'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalDialog from '@/core/ui/CalDialog.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalIcon from '@/core/ui/CalIcon.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'
import { toast } from '@/core/feedback/toasts'

/**
 * The per-surface design editor — the surface counterpart to the workspace
 * theme page. Empty fields inherit; a filled field overrides for this surface
 * only, and the reset arrow gives the inherited value back.
 */
const props = defineProps<{
  open: boolean
  workspaceKey: string
  surfaceKey: string
  canManage: boolean
}>()

const emit = defineEmits<{ 'update:open': [value: boolean]; changed: [] }>()

const api = useService('themesApi', themesApi)

const definitions = ref<ThemeDefinition[]>([])
const assignment = ref<SurfaceThemeAssignment | null>(null)
const settings = ref<SurfaceThemeSettings | null>(null)
const inputs = reactive<Record<string, string>>({})
const loading = ref(true)
const saving = ref(false)
const switching = ref(false)
const error = ref<string | null>(null)
const selectedTheme = ref('')

const activeFields = computed(() => (settings.value?.fields ?? []).filter((f) => f.isActive))
const inheritsWorkspaceValues = computed(() => settings.value?.inheritsWorkspaceValues ?? false)

// "" means "follow the workspace" — the same value the picker's first option carries.
const currentThemeValue = computed(() =>
  assignment.value && !assignment.value.inheritedFromWorkspace
    ? `${assignment.value.themePluginId}@${assignment.value.themeVersion}`
    : '',
)

function isOverridden(settingKey: string): boolean {
  return (inputs[settingKey] ?? '').trim() !== ''
}

// What the field falls back to: the workspace value if one applies, else the
// theme's default.
function inheritedPlaceholder(field: ThemeSettingDefinition): string {
  const inherited = settings.value?.inheritedValuesByKey[field.settingKey]
  if (inherited) {
    return displayJsonValue(inherited)
  }
  return displayJsonValue(field.defaultValueJson ?? '') || '(Default)'
}

function describeInheritance(settingKey: string): string | undefined {
  if (isOverridden(settingKey)) {
    return 'Für diese Surface überschrieben.'
  }
  return settings.value?.inheritedValuesByKey[settingKey] ? 'Wert des Workspaces.' : undefined
}

function reset(settingKey: string): void {
  inputs[settingKey] = ''
}

function hydrate(next: SurfaceThemeSettings): void {
  settings.value = next
  for (const key of Object.keys(inputs)) {
    delete inputs[key]
  }
  for (const field of next.fields) {
    // Only the surface's OWN values prefill — an inherited value stays a
    // placeholder, so saving does not silently copy it onto the surface.
    inputs[field.settingKey] = displayJsonValue(next.valuesByKey[field.settingKey] ?? '')
  }
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [defs, current, currentSettings] = await Promise.all([
      api.listDefinitions(),
      api.getSurfaceAssignment(props.workspaceKey, props.surfaceKey),
      api.getSurfaceSettings(props.workspaceKey, props.surfaceKey),
    ])
    definitions.value = defs
    assignment.value = current
    selectedTheme.value = current.inheritedFromWorkspace ? '' : `${current.themePluginId}@${current.themeVersion}`
    hydrate(currentSettings)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function applyTheme(): Promise<void> {
  if (switching.value) {
    return
  }
  error.value = null
  switching.value = true
  try {
    if (!selectedTheme.value) {
      await api.clearSurfaceAssignment(props.workspaceKey, props.surfaceKey)
      toast.success('Surface folgt wieder dem Theme des Workspaces.')
    } else {
      const [pluginId, version] = selectedTheme.value.split('@')
      await api.assignSurface(props.workspaceKey, props.surfaceKey, pluginId, version)
      toast.success('Theme der Surface gesetzt.')
    }
    await load()
    emit('changed')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    switching.value = false
  }
}

async function save(): Promise<void> {
  if (saving.value) {
    return
  }
  error.value = null
  // Only non-empty inputs travel: an emptied field drops the override and
  // returns the setting to the inherited value.
  const valuesByKey: Record<string, unknown> = {}
  for (const field of activeFields.value) {
    const raw = (inputs[field.settingKey] ?? '').trim()
    if (raw !== '') {
      valuesByKey[field.settingKey] = coerceInputToJsonValue(raw)
    }
  }
  saving.value = true
  try {
    hydrate(await api.saveSurfaceSettings(props.workspaceKey, props.surfaceKey, valuesByKey))
    toast.success('Werte der Surface gespeichert.')
    emit('changed')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

// Loaded on open, so the dialog always shows current state and closing it
// discards nothing the operator expected to keep.
watch(
  () => props.open,
  (open) => {
    if (open) {
      void load()
    }
  },
  { immediate: true },
)
</script>

<style scoped lang="scss">
.surface-theme__message {
  margin-bottom: var(--cal-space-4);
}

.surface-theme__skeletons {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-3);
}

.surface-theme__block + .surface-theme__block {
  margin-top: var(--cal-space-6);
  padding-top: var(--cal-space-5);
  border-top: 1px solid var(--cal-border-subtle);
}

.surface-theme__block-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cal-space-3);
  margin-bottom: var(--cal-space-3);
}

.surface-theme__block-title {
  font-size: var(--cal-text-base);
  font-weight: var(--cal-weight-semibold);
}

.surface-theme__hint {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.surface-theme__assign {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  margin-bottom: var(--cal-space-3);
}

.surface-theme__assign > :deep(.cal-field) {
  flex: 1;
  min-width: 0;
}

.surface-theme__fields {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-4);
}

.surface-theme__reset {
  display: flex;
  padding: 0;
  border: 0;
  background: none;
  color: var(--cal-text-muted);
  cursor: pointer;
}

.surface-theme__reset:hover {
  color: var(--cal-accent);
}
</style>
