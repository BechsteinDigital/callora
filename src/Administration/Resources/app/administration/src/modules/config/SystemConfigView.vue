<template>
  <section class="config">
    <header class="head">
      <h1>Konfiguration</h1>
      <div class="head-actions">
        <ExtensionSlot name="config.toolbar" />
      </div>
    </header>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="notice" class="notice">{{ notice }}</p>
    <p v-if="loading">Lädt…</p>

    <template v-else-if="plugins.length">
      <label class="plugin-select">Plugin
        <select v-model="selectedPlugin" name="plugin" class="select" @change="onPluginChange">
          <option v-for="p in plugins" :key="p" :value="p">{{ p }}</option>
        </select>
      </label>

      <p class="legend">
        Werte als JSON (<code>42</code>, <code>true</code>, <code>"Text"</code>). Reiner Text wird als Zeichenkette
        gespeichert. Leere Felder bleiben unverändert. Bereich: <strong>global</strong>.
      </p>

      <form class="fields" @submit.prevent="save">
        <div v-for="def in fields" :key="def.configKey" class="field">
          <label :for="`cfg-${def.configKey}`">
            {{ def.label }}
            <span v-if="def.groupName" class="group">{{ def.groupName }}</span>
          </label>
          <p v-if="def.description" class="desc">{{ def.description }}</p>
          <p class="current">Aktuell: <span class="mono">{{ effectiveDisplay(def) }}</span></p>
          <input
            :id="`cfg-${def.configKey}`"
            v-model="inputs[def.configKey]"
            :name="def.configKey"
            :type="isSecretField(def.fieldType) ? 'password' : 'text'"
            :placeholder="isSecretField(def.fieldType) ? '•••• zum Ändern' : 'leer = unverändert'"
            class="input"
            :disabled="!canEdit"
          />
        </div>

        <ExtensionSlot name="config.fields" :ctx="{ pluginId: selectedPlugin }" />

        <div v-if="canEdit" class="buttons">
          <BaseButton type="submit" :disabled="saving">{{ saving ? 'Speichert…' : 'Speichern' }}</BaseButton>
        </div>
        <p v-if="!fields.length" class="empty">Dieses Plugin hat keine Konfigurationsfelder.</p>
      </form>
    </template>

    <p v-else class="empty">Keine konfigurierbaren Plugins.</p>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { systemConfigApi, isSecretField, ConfigScope, type ConfigDefinition, type EffectiveConfig } from './systemConfigApi'
import { coerceInputToJsonValue, displayJsonValue } from './configValues'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

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
.config {
  padding: calc(var(--cal-space) * 3);
  max-width: 560px;
}

.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: calc(var(--cal-space) * 2);
}

.head-actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
}

.plugin-select {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
  margin-bottom: calc(var(--cal-space) * 1.5);
}

.select {
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.legend {
  font-size: 0.85em;
  color: var(--cal-color-muted);
  margin-bottom: calc(var(--cal-space) * 2);
}

.legend code {
  font-family: var(--cal-font-mono, monospace);
}

.fields {
  display: flex;
  flex-direction: column;
  gap: calc(var(--cal-space) * 2);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field label {
  color: var(--cal-color-text);
  font-weight: 600;
}

.group {
  font-size: 0.75em;
  font-weight: 400;
  color: var(--cal-color-muted);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  padding: 0 calc(var(--cal-space) * 0.75);
  margin-left: var(--cal-space);
}

.desc {
  font-size: 0.85em;
  color: var(--cal-color-muted);
  margin: 0;
}

.current {
  font-size: 0.85em;
  color: var(--cal-color-muted);
  margin: 0;
}

.mono {
  font-family: var(--cal-font-mono, monospace);
}

.input {
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
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
