<template>
  <section class="jobs">
    <header class="head">
      <h1>Jobs</h1>
      <div class="head-actions">
        <ExtensionSlot name="jobs.list.toolbar" />
        <label class="limit">
          Anzahl
          <select :value="limit" name="jobLimit" class="select" @change="onLimitChange">
            <option v-for="n in limitOptions" :key="n" :value="n">{{ n }}</option>
          </select>
        </label>
        <button type="button" class="link" :disabled="loading" @click="load">
          {{ loading ? 'Lädt…' : 'Aktualisieren' }}
        </button>
      </div>
    </header>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading && !jobs.length">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Typ</th>
          <th>Status</th>
          <th>Workspace</th>
          <th>Versuche</th>
          <th>Erstellt</th>
          <th>Abgeschlossen</th>
          <th>Fehler</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="job in jobs" :key="job.id">
          <td>{{ job.jobType }}</td>
          <td>
            <span class="badge" :class="`badge-${statusTone(job.status)}`">{{ job.status }}</span>
          </td>
          <td class="mono">{{ job.workspaceKey ?? '—' }}</td>
          <td>{{ job.attemptCount }} / {{ job.maxAttempts }}</td>
          <td>{{ formatTimestamp(job.createdAtUtc) }}</td>
          <td>{{ formatTimestamp(job.completedAtUtc) }}</td>
          <td class="err" :title="job.lastError ?? ''">{{ job.lastError ?? '—' }}</td>
        </tr>
        <tr v-if="!jobs.length">
          <td colspan="7" class="empty">Keine Jobs.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { jobsApi, type Job } from './jobsApi'
import { formatTimestamp, statusTone } from './jobsFormat'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'

const jobs = ref<Job[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const limitOptions = [25, 50, 100]
const limit = ref(limitOptions[0])

// Resolve the jobs service through the override registry: a plugin may replace it.
const api = useService('jobsApi', jobsApi)

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

// Read the committed DOM value directly so the reload always uses the new limit
// (avoids the v-model-vs-@change ordering pitfall).
function onLimitChange(event: Event): void {
  limit.value = Number((event.target as HTMLSelectElement).value)
  void load()
}

onMounted(load)
</script>

<style scoped lang="scss">
.jobs {
  padding: calc(var(--cal-space) * 3);
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
  gap: calc(var(--cal-space) * 1.5);
}

.limit {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
  color: var(--cal-color-muted);
  font-size: 0.9em;
}

.select {
  padding: calc(var(--cal-space) * 0.75) var(--cal-space);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.grid {
  width: 100%;
  border-collapse: collapse;
}

.grid th,
.grid td {
  text-align: left;
  padding: var(--cal-space);
  border-bottom: 1px solid var(--cal-color-surface);
  vertical-align: top;
}

.grid th {
  color: var(--cal-color-muted);
  font-weight: 600;
}

.mono {
  font-family: var(--cal-font-mono, monospace);
  color: var(--cal-color-muted);
}

.badge {
  font-size: 0.75em;
  border-radius: var(--cal-radius);
  padding: 0 calc(var(--cal-space) * 0.75);
  border: 1px solid currentColor;
}

.badge-success {
  color: var(--cal-color-accent);
}

.badge-danger {
  color: var(--cal-color-danger);
}

.badge-neutral {
  color: var(--cal-color-muted);
}

.err {
  max-width: 280px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--cal-color-danger);
}

.link {
  background: none;
  border: 0;
  color: var(--cal-color-accent);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link:disabled {
  opacity: 0.5;
  cursor: default;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
