<template>
  <CalPage>
    <CalPageHeader title="Übersicht" :description="greeting" />

    <GettingStartedCard v-if="showGettingStarted" class="dashboard__onboarding" />

    <section v-if="visibleMetrics.length" class="dashboard__kpis">
      <CalStat
        v-for="m in visibleMetrics"
        :key="m.key"
        class="kpi"
        :label="m.label"
        :icon="m.icon"
        :to="m.to"
        :value="valueOf(m.key)"
        :loading="values[m.key] === null"
        :unavailable="values[m.key] === 'error'"
        :caption="values[m.key] === 'error' ? 'Konnte nicht geladen werden' : m.caption"
      />
      <ExtensionSlot name="dashboard.metrics" :ctx="{ permissions: ctx?.permissions ?? [] }" />
    </section>

    <CalCard v-if="ctx" title="Ihre Sitzung" description="Womit Sie angemeldet sind — und was daraus folgt.">
      <CalDescriptionList :items="identityItems">
        <template #Operator>
          <CalBadge :tone="ctx.isOperator ? 'accent' : 'neutral'" dot>
            {{ ctx.isOperator ? 'ja' : 'nein' }}
          </CalBadge>
        </template>
        <template #Rollen>
          <span v-if="!ctx.roles.length">—</span>
          <span v-else class="dashboard__roles">
            <CalBadge v-for="role in ctx.roles" :key="role" tone="neutral">{{ role }}</CalBadge>
          </span>
        </template>
      </CalDescriptionList>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, type Component } from 'vue'
import { Boxes, Puzzle, Timer, Users } from 'lucide-vue-next'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import { usersApi } from '@/modules/users/usersApi'
import { workspacesApi } from '@/modules/workspaces/workspacesApi'
import { pluginsApi, isPluginActive } from '@/modules/plugins/pluginsApi'
import { jobsApi } from '@/modules/jobs/jobsApi'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import GettingStartedCard from '@/modules/onboarding/GettingStartedCard.vue'
import { useOnboarding } from '@/modules/onboarding/onboarding'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDescriptionList from '@/core/ui/CalDescriptionList.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalStat from '@/core/ui/CalStat.vue'
import type { DescriptionItem } from '@/core/ui/descriptionList'

const ctx = useAuthStore().context

const greeting = computed(() => {
  const name = ctx.value?.displayName ?? ctx.value?.userId
  return name ? `Angemeldet als ${name}.` : 'Der Zustand der Installation auf einen Blick.'
})

// The onboarding card stays on the dashboard until setup is complete or dismissed;
// only operators see it (a scoped admin has no fresh-install setup to do).
const { isReady: onboardingReady, isComplete: onboardingComplete, isDismissed: onboardingDismissed } = useOnboarding()
const showGettingStarted = computed(
  () =>
    (ctx.value?.isOperator ?? false) &&
    onboardingReady.value &&
    !onboardingComplete.value &&
    !onboardingDismissed.value,
)

// null = still loading, 'error' = load failed, number = the metric value.
type MetricValue = number | null | 'error'

interface Metric {
  readonly key: string
  readonly label: string
  readonly permission: string
  readonly icon: Component
  /** The list behind the figure — the tile links there. */
  readonly to: string
  readonly caption?: string
  readonly load: () => Promise<number>
}

// Each KPI mirrors the read gate of the endpoint it counts; a caller only sees
// the metrics they may actually read (the API stays authoritative regardless).
const metrics: readonly Metric[] = [
  {
    key: 'users',
    label: 'Benutzer',
    permission: 'user.read',
    icon: Users,
    to: '/users',
    load: async () => (await usersApi.list()).length,
  },
  {
    key: 'workspaces',
    label: 'Workspaces',
    permission: 'workspace.read',
    icon: Boxes,
    to: '/workspaces',
    load: async () => (await workspacesApi.list()).length,
  },
  {
    key: 'plugins',
    label: 'Aktive Plugins',
    permission: 'plugin.read',
    icon: Puzzle,
    to: '/plugins',
    load: async () => (await pluginsApi.list()).filter((p) => isPluginActive(p.state)).length,
  },
  {
    key: 'jobs',
    label: 'Jobs (aktuell)',
    permission: 'job.read',
    icon: Timer,
    to: '/jobs',
    caption: 'In der Warteschlange',
    load: async () => (await jobsApi.list()).length,
  },
]

const visibleMetrics = computed(() => metrics.filter((m) => hasPermission(ctx.value, m.permission)))
const values = reactive<Record<string, MetricValue>>({})

// A metric that failed must never render as a plausible number — CalStat shows
// the unavailable dash instead, and the caption says why.
function valueOf(key: string): number | null {
  const value = values[key]
  return typeof value === 'number' ? value : null
}

const identityItems = computed<DescriptionItem[]>(() => {
  const context = ctx.value
  if (!context) {
    return []
  }
  return [
    { term: 'Benutzer', value: `${context.displayName ?? context.userId} (${context.userId})` },
    { term: 'Scope', value: `${context.scope ?? '—'}${context.workspaceKey ? ` / ${context.workspaceKey}` : ''}` },
    { term: 'Rollen', value: context.roles.join(', ') },
    { term: 'Operator', value: context.isOperator ? 'ja' : 'nein' },
    { term: 'Permissions', value: context.permissions.length },
  ]
})

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
.dashboard__onboarding {
  margin-bottom: var(--cal-space-6);
}

.dashboard__kpis {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--cal-space-4);
  margin-bottom: var(--cal-space-6);
}

.dashboard__roles {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cal-space-1);
}
</style>
