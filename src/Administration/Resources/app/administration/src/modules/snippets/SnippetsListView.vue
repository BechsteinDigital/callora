<template>
  <CalPage>
    <CalPageHeader
      title="Texte"
      description="Was Core und Plugins an Beschriftungen mitbringen — und was Sie daraus machen."
    >
      <template #actions>
        <ExtensionSlot name="snippets.toolbar" />
      </template>
    </CalPageHeader>

    <CalAlert v-if="error" class="snippets__message" tone="danger">{{ error }}</CalAlert>
    <CalAlert v-if="notice" class="snippets__message" tone="success" dismissible @dismiss="notice = null">
      {{ notice }}
    </CalAlert>

    <CalCard class="snippets__picker">
      <div class="snippets__picker-fields">
        <CalField v-slot="{ id }" label="Sprache" description="Ein Schlüssel trägt seinen Text je Sprache.">
          <CalInput :id="id" v-model="locale" name="locale" placeholder="de" @change="load" />
        </CalField>

        <!--
          Gezeigt wird EINE Ebene, nie die aufgelöste Kette: Wer hier steht, soll sehen, was an
          dieser Stelle gesetzt ist. Sonst wäre nicht zu erkennen, was das Zurücknehmen bewirkt.
        -->
        <CalField
          v-slot="{ id }"
          label="Bereich"
          description="Ein Override gilt ab hier abwärts — global unter allem, ein Workspace nur bei sich."
        >
          <CalSelect :id="id" v-model="scope" name="scope" @update:model-value="load">
            <option value="global">Global</option>
            <option value="tenant">Mandant</option>
            <option value="workspace">Workspace</option>
          </CalSelect>
        </CalField>

        <CalField v-if="scope !== 'global'" v-slot="{ id }" :label="scope === 'tenant' ? 'Mandant' : 'Workspace'">
          <CalInput :id="id" v-model="scopeKey" name="scopeKey" @change="load" />
        </CalField>
      </div>
    </CalCard>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="rows"
        row-key="snippetKey"
        :loading="loading"
        :error="error"
        :empty-icon="Type"
        empty-title="Keine Texte für diese Auswahl."
        empty-description="Texte kommen aus den Paketen — ohne installiertes Plugin gibt es hier nichts zu ändern."
      >
        <template #cell-snippetKey="{ row }">
          <code class="snippets__key">{{ row.snippetKey }}</code>
          <CalBadge v-if="row.isOrphaned" tone="warning" variant="outline" title="Das Paket kennt diesen Schlüssel nicht mehr; Ihr Text bleibt erhalten.">
            verwaist
          </CalBadge>
        </template>

        <template #cell-baseValue="{ row }">
          <span class="snippets__base">{{ row.baseValue ?? '—' }}</span>
        </template>

        <template #cell-value="{ row }">
          <CalInput
            :model-value="draftOf(row)"
            :name="`value-${row.snippetKey}`"
            :aria-label="`Text für ${row.snippetKey}`"
            @update:model-value="(value: string) => (drafts[row.snippetKey] = value)"
          />
        </template>

        <template #cell-actions="{ row }">
          <div class="snippets__actions">
            <CalButton
              v-if="canUpdate"
              variant="ghost"
              size="sm"
              :disabled="busyKey === row.snippetKey || draftOf(row) === row.effectiveValue"
              @click="save(row)"
            >
              Speichern
            </CalButton>
            <!--
              Zurücknehmen heißt zurück zur Basis. Es gibt nichts wiederherzustellen — beim
              Anlegen eines Bereichs wurde nie etwas kopiert.
            -->
            <CalButton
              v-if="canUpdate && row.isOverridden"
              variant="danger-ghost"
              size="sm"
              :disabled="busyKey === row.snippetKey"
              @click="reset(row)"
            >
              Zurücksetzen
            </CalButton>
          </div>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Type } from 'lucide-vue-next'
import { snippetsApi, type Snippet, type SnippetScope } from './snippetsApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { toast } from '@/core/feedback/toasts'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSelect from '@/core/ui/CalSelect.vue'

const columns = [
  { key: 'snippetKey', label: 'Schlüssel' },
  { key: 'baseValue', label: 'Aus dem Paket' },
  { key: 'value', label: 'Text' },
  { key: 'actions', label: '', align: 'end' as const },
]

const ctx = useAuthStore().context
const canUpdate = computed(() => hasPermission(ctx.value, 'snippet.update'))

const locale = ref('de')
const scope = ref<SnippetScope['scope']>('global')
const scopeKey = ref('')
const rows = ref<Snippet[]>([])
const drafts = reactive<Record<string, string>>({})
const loading = ref(false)
const busyKey = ref<string | null>(null)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)

const target = computed<SnippetScope>(() => ({
  locale: locale.value.trim(),
  scope: scope.value,
  scopeKey: scopeKey.value.trim(),
}))

function draftOf(row: Snippet): string {
  return drafts[row.snippetKey] ?? row.effectiveValue
}

async function load(): Promise<void> {
  if (!locale.value.trim() || (scope.value !== 'global' && !scopeKey.value.trim())) {
    rows.value = []
    return
  }

  loading.value = true
  error.value = null
  try {
    rows.value = await snippetsApi.list(target.value)
    for (const key of Object.keys(drafts)) {
      delete drafts[key]
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function save(row: Snippet): Promise<void> {
  busyKey.value = row.snippetKey
  try {
    await snippetsApi.set(row.snippetKey, target.value, draftOf(row))
    toast.success(`„${row.snippetKey}“ gespeichert.`)
    await load()
  } catch (e) {
    toast.error(e)
  } finally {
    busyKey.value = null
  }
}

async function reset(row: Snippet): Promise<void> {
  busyKey.value = row.snippetKey
  try {
    await snippetsApi.reset(row.snippetKey, target.value)
    toast.success(`„${row.snippetKey}“ zurückgesetzt — der Text des Pakets gilt wieder.`)
    await load()
  } catch (e) {
    toast.error(e)
  } finally {
    busyKey.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.snippets__message {
  margin-bottom: var(--cal-space-4);
}

.snippets__picker {
  margin-bottom: var(--cal-space-4);
}

.snippets__picker-fields {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--cal-space-4);
}

.snippets__key {
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
  color: var(--cal-text-secondary);
  margin-inline-end: var(--cal-space-2);
}

.snippets__base {
  color: var(--cal-text-muted);
}

.snippets__actions {
  display: flex;
  gap: var(--cal-space-2);
  justify-content: flex-end;
}
</style>
