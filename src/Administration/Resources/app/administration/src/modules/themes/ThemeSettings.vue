<template>
  <section class="theme-settings">
    <h2>Theme-Einstellungen</h2>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="notice" class="notice">{{ notice }}</p>
    <p v-if="loading">Lädt…</p>

    <template v-else>
      <p v-if="!activeFields.length" class="empty">Dieses Theme stellt keine Einstellungen bereit.</p>

      <form v-else class="fields" @submit.prevent="save">
        <label v-for="field in activeFields" :key="field.settingKey" class="field">
          <span class="label">
            {{ field.label }}
            <span v-if="field.groupName" class="group">· {{ field.groupName }}</span>
          </span>
          <span v-if="field.description" class="desc">{{ field.description }}</span>
          <BaseInput
            v-model="inputs[field.settingKey]"
            :name="`theme-setting-${field.settingKey}`"
            :placeholder="displayJsonValue(field.defaultValueJson) || '(Default)'"
            :disabled="!canManage"
          />
        </label>

        <div v-if="canManage" class="buttons">
          <BaseButton type="submit" :disabled="saving">{{ saving ? 'Speichert…' : 'Speichern' }}</BaseButton>
        </div>
      </form>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { themesApi, type ThemeSettingDefinition } from './themesApi'
import { coerceInputToJsonValue, displayJsonValue } from './themesValues'
import BaseButton from '@/core/ui/BaseButton.vue'
import BaseInput from '@/core/ui/BaseInput.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const props = defineProps<{ workspaceKey: string; canManage: boolean }>()

const api = useService('themesApi', themesApi)

const fields = ref<ThemeSettingDefinition[]>([])
const inputs = reactive<Record<string, string>>({})
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const saving = ref(false)

const activeFields = computed(() =>
  fields.value.filter((f) => f.isActive).sort((a, b) => a.sortOrder - b.sortOrder),
)

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
.theme-settings {
  margin-top: calc(var(--cal-space) * 3);
}

.theme-settings h2 {
  font-size: 1.1em;
  margin-bottom: var(--cal-space);
}

.fields {
  display: flex;
  flex-direction: column;
  gap: calc(var(--cal-space) * 1.5);
  max-width: 460px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.label {
  color: var(--cal-color-text);
}

.group {
  color: var(--cal-color-muted);
  font-size: 0.85em;
}

.desc {
  color: var(--cal-color-muted);
  font-size: 0.85em;
}

.buttons {
  margin-top: var(--cal-space);
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}

.notice {
  color: var(--cal-color-accent);
}
</style>
