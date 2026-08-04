<template>
  <CalPage wide>
    <CalPageHeader title="Jobs" description="Hintergrundaufgaben der Plattform, neueste zuerst.">
      <template #actions>
        <ExtensionSlot name="jobs.list.toolbar" />
        <label class="jobs__limit">
          <span>Anzahl</span>
          <CalSelect :model-value="String(limit)" name="jobLimit" size="sm" @update:model-value="onLimitChange">
            <option v-for="n in limitOptions" :key="n" :value="n">{{ n }}</option>
          </CalSelect>
        </label>
        <CalButton :icon="RefreshCw" :loading="loading" @click="load">Aktualisieren</CalButton>
      </template>
    </CalPageHeader>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="jobs"
        row-key="id"
        :loading="loading && !jobs.length"
        :error="error"
        :empty-icon="Timer"
        empty-title="Keine Jobs."
        empty-description="Sobald die Plattform Arbeit in die Warteschlange stellt, erscheint sie hier."
      >
        <template #cell-status="{ row }">
          <CalBadge :tone="badgeTone(row.status)" dot>{{ row.status }}</CalBadge>
        </template>

        <template #cell-attempts="{ row }">
          <span :class="{ 'jobs__attempts-exhausted': row.attemptCount >= row.maxAttempts }">
            {{ row.attemptCount }} / {{ row.maxAttempts }}
          </span>
        </template>

        <template #cell-createdAtUtc="{ row }">{{ formatTimestamp(row.createdAtUtc) }}</template>
        <template #cell-completedAtUtc="{ row }">{{ formatTimestamp(row.completedAtUtc) }}</template>

        <template #cell-lastError="{ row }">
          <span v-if="row.lastError" class="jobs__error" :title="row.lastError">{{ row.lastError }}</span>
          <span v-else>—</span>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RefreshCw, Timer } from 'lucide-vue-next'
import { jobsApi, type Job } from './jobsApi'
import { formatTimestamp, statusTone } from './jobsFormat'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'

const jobs = ref<Job[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const limitOptions = [25, 50, 100]
const limit = ref(limitOptions[0])

const columns: readonly DataTableColumn[] = [
  { key: 'jobType', label: 'Typ' },
  { key: 'status', label: 'Status', width: '130px' },
  { key: 'workspaceKey', label: 'Workspace', mono: true, width: '160px' },
  { key: 'attempts', label: 'Versuche', width: '110px' },
  { key: 'createdAtUtc', label: 'Erstellt', width: '170px' },
  { key: 'completedAtUtc', label: 'Abgeschlossen', width: '170px' },
  { key: 'lastError', label: 'Fehler' },
]

// Resolve the jobs service through the override registry: a plugin may replace it.
const api = useService('jobsApi', jobsApi)

// jobsFormat classifies the raw status; the badge only maps that onto a tone.
function badgeTone(status: string): 'success' | 'danger' | 'neutral' {
  return statusTone(status) as 'success' | 'danger' | 'neutral'
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    jobs.value = await api.list(limit.value)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

function onLimitChange(value: string): void {
  limit.value = Number(value)
  void load()
}

onMounted(load)
</script>

<style scoped lang="scss">
.jobs__limit {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
}

.jobs__limit :deep(.cal-select) {
  width: 76px;
}

.jobs__attempts-exhausted {
  color: var(--cal-warning);
  font-variant-numeric: tabular-nums;
}

.jobs__error {
  display: block;
  max-width: 320px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--cal-danger);
}
</style>
