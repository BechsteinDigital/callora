<template>
  <CalPage narrow>
    <CalPageHeader title="Konfiguration" description="Einstellungen der installierten Plugins auf Plattform-Ebene.">
      <template #actions>
        <ExtensionSlot name="config.toolbar" />
      </template>
    </CalPageHeader>

    <CalAlert v-if="error" class="config__message" tone="danger">{{ error }}</CalAlert>
    <CalAlert v-if="notice" class="config__message" tone="success" dismissible @dismiss="notice = null">
      {{ notice }}
    </CalAlert>

    <CalCard v-if="loading">
      <div class="config__skeletons">
        <CalSkeleton v-for="n in 4" :key="n" height="36px" />
      </div>
    </CalCard>

    <template v-else-if="plugins.length">
      <CalCard class="config__picker">
        <CalField v-slot="{ id }" label="Plugin" horizontal>
          <CalSelect :id="id" v-model="selectedPlugin" name="plugin" @update:model-value="onPluginChange">
            <option v-for="p in plugins" :key="p" :value="p">{{ p }}</option>
          </CalSelect>
        </CalField>
      </CalCard>

      <CalCard
        title="Werte"
        description="Als JSON eingeben (42, true, &quot;Text&quot;); reiner Text wird als Zeichenkette gespeichert. Leere Felder bleiben unverändert."
      >
        <form class="config__fields" @submit.prevent="save">
          <CalField
            v-for="def in fields"
            :key="def.configKey"
            v-slot="{ id }"
            :label="def.label"
            :hint="def.groupName || undefined"
            :description="def.description || undefined"
          >
            <CalInput
              :id="id"
              v-model="inputs[def.configKey]"
              :name="def.configKey"
              :type="isSecretField(def.fieldType) ? 'password' : 'text'"
              :placeholder="isSecretField(def.fieldType) ? '•••• zum Ändern' : 'leer = unverändert'"
              :disabled="!canEdit"
            >
              <template #suffix>
                <span class="config__current" :title="`Aktuell: ${effectiveDisplay(def)}`">
                  {{ effectiveDisplay(def) }}
                </span>
              </template>
            </CalInput>
          </CalField>

          <ExtensionSlot name="config.fields" :ctx="{ pluginId: selectedPlugin }" />

          <CalEmptyState v-if="!fields.length" compact title="Dieses Plugin hat keine Konfigurationsfelder." />
        </form>

        <template v-if="canEdit && fields.length" #footer>
          <div class="buttons">
            <CalButton variant="primary" :loading="saving" @click="save">Speichern</CalButton>
          </div>
        </template>
      </CalCard>
    </template>

    <CalCard v-else>
      <CalEmptyState
        :icon="Settings"
        title="Keine Konfiguration vorhanden."
        description="Sobald ein installiertes Plugin Konfigurationsfelder beiträgt, erscheinen sie hier."
      />
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Settings } from 'lucide-vue-next'
import {
  systemConfigApi,
  isSecretField,
  ConfigScope,
  type ConfigDefinition,
  type EffectiveConfig,
} from './systemConfigApi'
import { coerceInputToJsonValue, displayJsonValue } from './configValues'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'

const definitions = ref<ConfigDefinition[]>([])
const effective = ref<EffectiveConfig | null>(null)
const selectedPlugin = ref('')
const inputs = reactive<Record<string, string>>({})
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const saving = ref(false)

const ctx = useAuthStore().context
const canEdit = computed(() => hasPermission(ctx.value, 'config.update'))

// Resolve the config service through the override registry: a plugin may replace it.
const api = useService('systemConfigApi', systemConfigApi)

const plugins = computed(() => [...new Set(definitions.value.map((d) => d.pluginId))].sort())

const fields = computed(() =>
  definitions.value
    .filter((d) => d.pluginId === selectedPlugin.value && d.isActive)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.configKey.localeCompare(b.configKey)),
)

function effectiveDisplay(def: ConfigDefinition): string {
  if (!effective.value) {
    return '—'
  }
  const raw = effective.value.valuesByKey[def.configKey]
  if (isSecretField(def.fieldType)) {
    return raw ? '•••• (gesetzt)' : '— (nicht gesetzt)'
  }
  return displayJsonValue(raw)
}

function resetInputs(): void {
  for (const key of Object.keys(inputs)) {
    delete inputs[key]
  }
  for (const def of fields.value) {
    inputs[def.configKey] = ''
  }
}

async function loadPlugin(pluginId: string): Promise<void> {
  if (!pluginId) {
    return
  }
  error.value = null
  try {
    effective.value = await api.effective(pluginId)
    resetInputs()
  } catch (e) {
    error.value = (e as Error).message
  }
}

async function onPluginChange(): Promise<void> {
  notice.value = null
  await loadPlugin(selectedPlugin.value)
}

// The values map is mutable so a before-save hook can add or adjust entries;
// the plugin and scope are read-only context.
interface ConfigSaveDraft {
  readonly pluginId: string
  readonly scope: string
  values: Record<string, unknown>
}

// Only non-blank inputs are sent (per-key merge → blank leaves the value
// untouched); secrets are sent as their plaintext string, others coerced to JSON.
function buildValues(): Record<string, unknown> {
  const out: Record<string, unknown> = {}
  for (const def of fields.value) {
    const raw = (inputs[def.configKey] ?? '').trim()
    if (!raw) {
      continue
    }
    out[def.configKey] = isSecretField(def.fieldType) ? raw : coerceInputToJsonValue(raw)
  }
  return out
}

async function save(): Promise<void> {
  error.value = null
  notice.value = null
  const values = buildValues()
  if (Object.keys(values).length === 0) {
    notice.value = 'Keine Änderungen.'
    return
  }
  const draft: ConfigSaveDraft = { pluginId: selectedPlugin.value, scope: ConfigScope.Global, values }
  const before = await runHook('config.before-save', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }
  saving.value = true
  try {
    await api.saveValues(draft.pluginId, draft.scope, null, draft.values)
    await runHook('config.after-save', { pluginId: draft.pluginId, scope: draft.scope })
    await loadPlugin(selectedPlugin.value)
    notice.value = 'Konfiguration gespeichert.'
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  loading.value = true
  try {
    definitions.value = await api.listDefinitions()
    selectedPlugin.value = plugins.value[0] ?? ''
    if (selectedPlugin.value) {
      await loadPlugin(selectedPlugin.value)
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
.config__message {
  margin-bottom: var(--cal-space-4);
}

.config__picker {
  margin-bottom: var(--cal-space-4);
}

.config__skeletons {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-4);
}

.config__fields {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-5);
}

/* The effective value sits inside the field so the operator sees what they are
   about to overwrite, without a separate line per setting. */
.config__current {
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-xs);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
