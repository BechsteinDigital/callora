<template>
  <section class="webhooks">
    <header class="head">
      <h1>Webhooks</h1>
      <div class="head-actions">
        <ExtensionSlot name="webhooks.list.toolbar" />
      </div>
    </header>

    <form v-if="canManage" class="create" @submit.prevent="create">
      <div class="row">
        <input v-model="form.eventName" name="eventName" class="create-input" placeholder="Event (z. B. workspace.created)" />
        <input v-model="form.targetUrl" name="targetUrl" class="create-input wide" placeholder="Ziel-URL (https://…)" />
      </div>
      <div class="row">
        <input
          v-model="form.secret"
          name="secret"
          type="password"
          class="create-input"
          placeholder="Signatur-Secret"
          autocomplete="new-password"
        />
        <input v-model="form.workspaceKey" name="workspaceKey" class="create-input" placeholder="Workspace (optional)" />
        <label class="check">
          <input type="checkbox" v-model="form.includeSensitiveData" name="includeSensitiveData" />
          Sensible Daten senden
        </label>
        <BaseButton type="submit" :disabled="creating || !canSubmit">
          {{ creating ? 'Legt an…' : 'Anlegen' }}
        </BaseButton>
      </div>
    </form>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Event</th>
          <th>Ziel-URL</th>
          <th>Workspace</th>
          <th>Sensibel</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="w in webhooks" :key="w.id">
          <td class="mono">{{ w.eventName }}</td>
          <td class="url">{{ w.targetUrl }}</td>
          <td class="mono">{{ w.workspaceKey ?? '—' }}</td>
          <td>{{ w.includeSensitiveData ? 'Ja' : 'Nein' }}</td>
          <td>
            <span class="badge" :class="w.isActive ? 'badge-active' : 'badge-inactive'">
              {{ w.isActive ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
          <td class="actions">
            <button
              v-if="canManage"
              type="button"
              class="link"
              :disabled="busyId === w.id"
              @click="toggle(w)"
            >
              {{ w.isActive ? 'Deaktivieren' : 'Aktivieren' }}
            </button>
            <button
              v-if="canManage"
              type="button"
              class="link-danger"
              :disabled="busyId === w.id"
              @click="remove(w)"
            >
              Löschen
            </button>
            <ExtensionSlot name="webhooks.list.row-actions" :ctx="w" />
          </td>
        </tr>
        <tr v-if="!webhooks.length">
          <td colspan="6" class="empty">Keine Webhooks.</td>
        </tr>
      </tbody>
    </table>

    <div v-if="!loading && nextCursor" class="more">
      <button type="button" class="link" :disabled="loadingMore" @click="loadMore">
        {{ loadingMore ? 'Lädt…' : `Mehr laden (${webhooks.length}${total ? ` von ${total}` : ''})` }}
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { webhooksApi, type WebhookSubscription } from './webhooksApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

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
  if (!window.confirm(`Webhook „${webhook.eventName}“ → ${webhook.targetUrl} löschen?`)) {
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
.webhooks {
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
  gap: var(--cal-space);
}

.create {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space);
  margin-bottom: calc(var(--cal-space) * 2);
}

.create .row {
  display: flex;
  gap: var(--cal-space);
  align-items: center;
  flex-wrap: wrap;
}

.create-input {
  flex: 1;
  min-width: 180px;
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.create-input.wide {
  flex: 2;
}

.check {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
  color: var(--cal-color-muted);
  white-space: nowrap;
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
}

.grid th {
  color: var(--cal-color-muted);
  font-weight: 600;
}

.mono {
  font-family: var(--cal-font-mono, monospace);
  color: var(--cal-color-muted);
}

.url {
  max-width: 320px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.badge {
  font-size: 0.75em;
  border-radius: var(--cal-radius);
  padding: 0 calc(var(--cal-space) * 0.75);
  border: 1px solid currentColor;
}

.badge-active {
  color: var(--cal-color-accent);
}

.badge-inactive {
  color: var(--cal-color-muted);
}

.actions {
  display: flex;
  gap: calc(var(--cal-space) * 1.5);
  align-items: center;
}

.link {
  background: none;
  border: 0;
  color: var(--cal-color-accent);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link-danger {
  background: none;
  border: 0;
  color: var(--cal-color-danger);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link:disabled,
.link-danger:disabled {
  opacity: 0.5;
  cursor: default;
}

.more {
  margin-top: var(--cal-space);
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
