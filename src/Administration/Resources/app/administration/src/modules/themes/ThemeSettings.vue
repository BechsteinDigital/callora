<template>
  <CalCard title="Theme-Einstellungen" description="Werte, die das zugewiesene Theme zur Anpassung anbietet.">
    <CalAlert v-if="error" class="settings__message" tone="danger">{{ error }}</CalAlert>
    <CalAlert v-if="notice" class="settings__message" tone="success" dismissible @dismiss="notice = null">
      {{ notice }}
    </CalAlert>

    <div v-if="loading" class="settings__skeletons">
      <CalSkeleton v-for="n in 3" :key="n" height="36px" />
    </div>

    <CalEmptyState v-else-if="!activeFields.length" compact title="Dieses Theme stellt keine Einstellungen bereit." />

    <form v-else class="settings__fields" @submit.prevent="save">
      <CalField
        v-for="field in activeFields"
        :key="field.settingKey"
        v-slot="{ id }"
        :label="field.label"
        :hint="field.groupName || undefined"
        :description="field.description || undefined"
      >
        <CalInput
          :id="id"
          v-model="inputs[field.settingKey]"
          :name="`theme-setting-${field.settingKey}`"
          :placeholder="displayJsonValue(field.defaultValueJson) || '(Default)'"
          :disabled="!canManage"
        />
      </CalField>
    </form>

    <template v-if="canManage && activeFields.length" #footer>
      <div class="buttons">
        <CalButton variant="primary" :loading="saving" @click="save">Speichern</CalButton>
      </div>
    </template>
  </CalCard>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { themesApi, type ThemeSettingDefinition } from './themesApi'
import { coerceInputToJsonValue, displayJsonValue } from './themesValues'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'

const props = defineProps<{ workspaceKey: string; canManage: boolean }>()

const api = useService('themesApi', themesApi)

const fields = ref<ThemeSettingDefinition[]>([])
const inputs = reactive<Record<string, string>>({})
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const saving = ref(false)

const activeFields = computed(() => fields.value.filter((f) => f.isActive).sort((a, b) => a.sortOrder - b.sortOrder))

function hydrate(defs: ThemeSettingDefinition[], valuesByKey: Record<string, string>): void {
  fields.value = defs
  for (const key of Object.keys(inputs)) {
    delete inputs[key]
  }
  for (const field of defs) {
    inputs[field.settingKey] = displayJsonValue(valuesByKey[field.settingKey])
  }
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const settings = await api.getSettings(props.workspaceKey)
    hydrate(settings.fields, settings.valuesByKey)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function save(): Promise<void> {
  if (saving.value) {
    return
  }
  error.value = null
  notice.value = null
  // Only non-empty inputs are sent; an empty field is omitted so it falls back to
  // the theme default (the backend replaces the whole value set).
  const valuesByKey: Record<string, unknown> = {}
  for (const field of activeFields.value) {
    const raw = inputs[field.settingKey]?.trim() ?? ''
    if (raw !== '') {
      valuesByKey[field.settingKey] = coerceInputToJsonValue(raw)
    }
  }
  const before = await runHook('themes.settings.before-save', { workspaceKey: props.workspaceKey, valuesByKey })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }
  saving.value = true
  try {
    const settings = await api.saveSettings(props.workspaceKey, valuesByKey)
    hydrate(settings.fields, settings.valuesByKey)
    await runHook('themes.settings.after-save', { workspaceKey: props.workspaceKey })
    notice.value = 'Einstellungen gespeichert.'
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.settings__message {
  margin-bottom: var(--cal-space-4);
}

.settings__skeletons,
.settings__fields {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-4);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
