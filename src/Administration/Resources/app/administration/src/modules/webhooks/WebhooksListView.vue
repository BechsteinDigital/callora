<template>
  <CalListPage
    module="webhooks"
    title="Webhooks"
    description="Ereignisse der Plattform an externe Ziele zustellen."
  >

    <CalCard
      v-if="canManage"
      class="webhooks__create"
      title="Webhook anlegen"
      description="Das Secret signiert jede Zustellung und wird danach nie wieder angezeigt."
    >
      <form class="webhooks__form" @submit.prevent="create">
        <div class="webhooks__row">
          <CalField v-slot="{ id }" label="Event">
            <CalInput :id="id" v-model="form.eventName" name="eventName" placeholder="workspace.created" />
          </CalField>
          <CalField v-slot="{ id }" class="webhooks__grow" label="Ziel-URL">
            <CalInput :id="id" v-model="form.targetUrl" name="targetUrl" placeholder="https://…" />
          </CalField>
        </div>
        <div class="webhooks__row">
          <CalField v-slot="{ id }" label="Signatur-Secret">
            <CalInput
              :id="id"
              v-model="form.secret"
              name="secret"
              type="password"
              autocomplete="new-password"
              :icon="KeyRound"
            />
          </CalField>
          <CalField v-slot="{ id }" label="Workspace" hint="optional">
            <CalInput :id="id" v-model="form.workspaceKey" name="workspaceKey" />
          </CalField>
        </div>
        <div class="webhooks__row is-actions">
          <CalCheckbox v-model="form.includeSensitiveData" name="includeSensitiveData">
            Sensible Daten senden
          </CalCheckbox>
          <CalButton type="submit" variant="primary" :icon="Plus" :loading="creating" :disabled="!canSubmit">
            Anlegen
          </CalButton>
        </div>
      </form>
    </CalCard>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="webhooks"
        row-key="id"
        :loading="loading"
        :error="error"
        :empty-icon="Webhook"
        empty-title="Keine Webhooks."
        empty-description="Legen Sie ein Abonnement an, um Ereignisse an ein externes System zu melden."
      >
        <template #cell-includeSensitiveData="{ row }">
          <CalBadge :tone="row.includeSensitiveData ? 'warning' : 'neutral'">
            {{ row.includeSensitiveData ? 'Ja' : 'Nein' }}
          </CalBadge>
        </template>

        <template #cell-isActive="{ row }">
          <CalBadge :tone="row.isActive ? 'success' : 'neutral'" dot>
            {{ row.isActive ? 'Aktiv' : 'Inaktiv' }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="webhooks__actions">
            <CalButton v-if="canManage" variant="ghost" size="sm" :disabled="busyId === row.id" @click="toggle(row)">
              {{ row.isActive ? 'Deaktivieren' : 'Aktivieren' }}
            </CalButton>
            <CalButton
              v-if="canManage"
              variant="danger-ghost"
              size="sm"
              :disabled="busyId === row.id"
              @click="remove(row)"
            >
              Löschen
            </CalButton>
            <ExtensionSlot name="webhooks.list.row-actions" :ctx="row" />
          </div>
        </template>
      </CalDataTable>

      <template v-if="!loading && nextCursor" #footer>
        <CalButton :loading="loadingMore" @click="loadMore">
          Mehr laden ({{ webhooks.length }}{{ total ? ` von ${total}` : '' }})
        </CalButton>
      </template>
    </CalCard>
  </CalListPage>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { KeyRound, Plus, Webhook } from 'lucide-vue-next'
import { webhooksApi, type WebhookSubscription } from './webhooksApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import CalListPage from '@/core/patterns/CalListPage.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalCheckbox from '@/core/ui/CalCheckbox.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

const webhooks = ref<WebhookSubscription[]>([])
const loading = ref(true)
const loadingMore = ref(false)
const error = ref<string | null>(null)
const total = ref(0)
const nextCursor = ref<string | null>(null)
const busyId = ref<string | null>(null)
const creating = ref(false)

const form = reactive({
  eventName: '',
  targetUrl: '',
  secret: '',
  workspaceKey: '',
  includeSensitiveData: false,
})

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'webhook.manage'))
const canSubmit = computed(
  () => form.eventName.trim() !== '' && form.targetUrl.trim() !== '' && form.secret.trim() !== '',
)

const columns: readonly DataTableColumn[] = [
  { key: 'eventName', label: 'Event', mono: true },
  { key: 'targetUrl', label: 'Ziel-URL' },
  { key: 'workspaceKey', label: 'Workspace', mono: true, width: '150px' },
  { key: 'includeSensitiveData', label: 'Sensibel', width: '110px' },
  { key: 'isActive', label: 'Status', width: '120px' },
  { key: 'actions', label: '', align: 'end', width: '230px' },
]

// Resolve the webhooks service through the override registry: a plugin may replace it.
const api = useService('webhooksApi', webhooksApi)

// (Re)loads from the first page.
async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const page = await api.list()
    webhooks.value = page.items
    total.value = page.total
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function loadMore(): Promise<void> {
  if (!nextCursor.value || loadingMore.value) {
    return
  }
  loadingMore.value = true
  error.value = null
  try {
    const page = await api.list(nextCursor.value)
    webhooks.value = [...webhooks.value, ...page.items]
    total.value = page.total
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loadingMore.value = false
  }
}

async function create(): Promise<void> {
  if (!canSubmit.value) {
    return
  }
  error.value = null
  // The secret is deliberately kept OUT of the hook payload — a plugin handler may
  // enrich/veto the subscription, but must never see the raw signing secret. It is
  // merged back in only for the API call.
  const draft = {
    eventName: form.eventName.trim(),
    targetUrl: form.targetUrl.trim(),
    workspaceKey: form.workspaceKey.trim() || null,
    includeSensitiveData: form.includeSensitiveData,
  }
  const before = await runHook('webhooks.before-create', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Anlegen abgebrochen.'
    return
  }
  creating.value = true
  try {
    await api.create({ ...draft, secret: form.secret })
    await runHook('webhooks.after-create', { eventName: draft.eventName })
    toast.success(`Webhook für „${draft.eventName}“ angelegt.`)
    form.eventName = ''
    form.targetUrl = ''
    form.secret = ''
    form.workspaceKey = ''
    form.includeSensitiveData = false
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    creating.value = false
  }
}

async function toggle(webhook: WebhookSubscription): Promise<void> {
  if (busyId.value === webhook.id) {
    return
  }
  error.value = null
  busyId.value = webhook.id
  try {
    await api.setActive(webhook.id, !webhook.isActive)
    toast.success(webhook.isActive ? 'Webhook deaktiviert.' : 'Webhook aktiviert.')
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

async function remove(webhook: WebhookSubscription): Promise<void> {
  if (busyId.value === webhook.id) {
    return
  }
  const confirmed = await confirm({
    title: `Webhook „${webhook.eventName}“ löschen?`,
    description: `Zustellungen an ${webhook.targetUrl} enden sofort.`,
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  error.value = null
  const before = await runHook('webhooks.before-delete', { id: webhook.id })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  busyId.value = webhook.id
  try {
    await api.remove(webhook.id)
    await runHook('webhooks.after-delete', { id: webhook.id })
    toast.success('Webhook gelöscht.')
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.webhooks__create {
  margin-bottom: var(--cal-space-4);
}

.webhooks__form {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-4);
}

.webhooks__row {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-4);
  flex-wrap: wrap;
}

.webhooks__row > :deep(.cal-field) {
  flex: 1;
  min-width: 220px;
}

.webhooks__grow {
  flex: 2 !important;
}

.webhooks__row.is-actions {
  justify-content: space-between;
  align-items: center;
}

.webhooks__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}
</style>
