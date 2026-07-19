<template>
  <section class="dashboard">
    <h1>Übersicht</h1>

    <div class="kpis">
      <article v-for="m in visibleMetrics" :key="m.key" class="kpi">
        <span class="kpi-value" :class="{ err: values[m.key] === 'error' }">{{ display(values[m.key]) }}</span>
        <span class="kpi-label">{{ m.label }}</span>
      </article>
      <ExtensionSlot name="dashboard.metrics" :ctx="{ permissions: ctx?.permissions ?? [] }" />
    </div>

    <dl v-if="ctx" class="identity">
      <dt>Benutzer</dt>
      <dd>{{ ctx.displayName ?? ctx.userId }} ({{ ctx.userId }})</dd>
      <dt>Scope</dt>
      <dd>{{ ctx.scope ?? '—' }}{{ ctx.workspaceKey ? ` / ${ctx.workspaceKey}` : '' }}</dd>
      <dt>Rollen</dt>
      <dd>{{ ctx.roles.join(', ') || '—' }}</dd>
      <dt>Operator</dt>
      <dd>{{ ctx.isOperator ? 'ja' : 'nein' }}</dd>
      <dt>Permissions</dt>
      <dd>{{ ctx.permissions.length }}</dd>
    </dl>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive } from 'vue'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import { usersApi } from '@/modules/users/usersApi'
import { workspacesApi } from '@/modules/workspaces/workspacesApi'
import { pluginsApi, isPluginActive } from '@/modules/plugins/pluginsApi'
import { jobsApi } from '@/modules/jobs/jobsApi'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'

const ctx = useAuthStore().context

// null = still loading, 'error' = load failed, number = the metric value.
type MetricValue = number | null | 'error'

interface Metric {
  readonly key: string
  readonly label: string
  readonly permission: string
  readonly load: () => Promise<number>
}

// Each KPI mirrors the read gate of the endpoint it counts; a caller only sees
// the metrics they may actually read (the API stays authoritative regardless).
const metrics: readonly Metric[] = [
  { key: 'users', label: 'Benutzer', permission: 'user.read', load: async () => (await usersApi.list()).length },
  { key: 'workspaces', label: 'Workspaces', permission: 'workspace.read', load: async () => (await workspacesApi.list()).length },
  {
    key: 'plugins',
    label: 'Aktive Plugins',
    permission: 'plugin.read',
    load: async () => (await pluginsApi.list()).filter((p) => isPluginActive(p.state)).length,
  },
  { key: 'jobs', label: 'Jobs (aktuell)', permission: 'job.read', load: async () => (await jobsApi.list()).length },
]

const visibleMetrics = computed(() => metrics.filter((m) => hasPermission(ctx.value, m.permission)))
const values = reactive<Record<string, MetricValue>>({})

function display(value: MetricValue): string {
  if (value === 'error') {
    return '—'
  }
  return value === null || value === undefined ? '…' : String(value)
}

onMounted(() => {
  for (const metric of visibleMetrics.value) {
    values[metric.key] = null
    metric
      .load()
      .then((count) => {
        values[metric.key] = count
      })
      .catch(() => {
        values[metric.key] = 'error'
      })
  }
})
</script>

<style scoped lang="scss">
.dashboard {
  padding: calc(var(--cal-space) * 3);
}

.kpis {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: calc(var(--cal-space) * 2);
  margin: calc(var(--cal-space) * 2) 0 calc(var(--cal-space) * 3);
  max-width: 720px;
}

.kpi {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: calc(var(--cal-space) * 2);
  border: 1px solid var(--cal-color-surface);
  border-radius: var(--cal-radius);
}

.kpi-value {
  font-size: 1.8em;
  font-weight: 700;
  color: var(--cal-color-accent);
}

.kpi-value.err {
  color: var(--cal-color-muted);
}

.kpi-label {
  color: var(--cal-color-muted);
  font-size: 0.9em;
}

.identity {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: var(--cal-space) calc(var(--cal-space) * 2);
  max-width: 480px;
}

.identity dt {
  color: var(--cal-color-muted);
}
</style>
