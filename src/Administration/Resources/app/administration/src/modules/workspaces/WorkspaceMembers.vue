<template>
  <section class="members">
    <h2>Mitglieder</h2>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Benutzer</th>
          <th>Rolle</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="m in members" :key="m.userId">
          <td>
            {{ m.displayName || m.userId }}
            <span v-if="m.displayName" class="sub mono">{{ m.userId }}</span>
          </td>
          <td>{{ m.role }}</td>
          <td class="actions">
            <button
              v-if="canManage"
              type="button"
              class="link-danger"
              :disabled="busyUserId === m.userId"
              @click="remove(m)"
            >
              Entfernen
            </button>
            <ExtensionSlot name="workspaces.members.row-actions" :ctx="m" />
          </td>
        </tr>
        <tr v-if="!members.length">
          <td colspan="3" class="empty">Keine Mitglieder.</td>
        </tr>
      </tbody>
    </table>

    <div v-if="!loading && nextCursor" class="more">
      <button type="button" class="link" :disabled="loadingMore" @click="loadMore">
        {{ loadingMore ? 'Lädt…' : `Mehr laden (${members.length}${total ? ` von ${total}` : ''})` }}
      </button>
    </div>

    <form v-if="canManage" class="add" @submit.prevent="add">
      <input v-model="userId" name="memberUserId" class="add-input" placeholder="Benutzer-Login" />
      <input v-model="role" name="memberRole" class="add-input" placeholder="Rolle" />
      <BaseButton type="submit" :disabled="adding || !userId.trim() || !role.trim()">Zuweisen</BaseButton>
    </form>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { workspacesApi, type WorkspaceMember } from './workspacesApi'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const props = defineProps<{ workspaceKey: string; canManage: boolean }>()

const members = ref<WorkspaceMember[]>([])
const loading = ref(true)
const loadingMore = ref(false)
const error = ref<string | null>(null)
const total = ref(0)
const nextCursor = ref<string | null>(null)
const userId = ref('')
const role = ref('')
const adding = ref(false)
const busyUserId = ref<string | null>(null)

// Resolve the workspaces service through the override registry: a plugin may replace it.
const api = useService('workspacesApi', workspacesApi)

// (Re)loads from the first page.
async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const page = await api.listMembers(props.workspaceKey)
    members.value = page.items
    total.value = page.total
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

// Fetches the next page (cursor-based) and appends it to the current list.
async function loadMore(): Promise<void> {
  if (!nextCursor.value || loadingMore.value) {
    return
  }
  loadingMore.value = true
  error.value = null
  try {
    const page = await api.listMembers(props.workspaceKey, nextCursor.value)
    members.value = [...members.value, ...page.items]
    total.value = page.total
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loadingMore.value = false
  }
}

// A before-save hook may enrich the role or veto; the user id is the read-only
// identity of who is being assigned.
interface MemberDraft {
  readonly userId: string
  role: string
}

async function add(): Promise<void> {
  const id = userId.value.trim()
  const roleName = role.value.trim()
  if (!id || !roleName) {
    return
  }
  error.value = null
  const draft: MemberDraft = { userId: id, role: roleName }
  const before = await runHook('workspaces.member.before-save', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Zuweisung abgebrochen.'
    return
  }
  adding.value = true
  try {
    await api.upsertMember(props.workspaceKey, draft.userId, draft.role)
    await runHook('workspaces.member.after-save', { workspaceKey: props.workspaceKey, userId: id })
    userId.value = ''
    role.value = ''
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    adding.value = false
  }
}

async function remove(member: WorkspaceMember): Promise<void> {
  // Guard re-entry: each row is its own trigger, so a double-click must not fire
  // two DELETEs / two reloads for the same member.
  if (busyUserId.value === member.userId) {
    return
  }
  if (!window.confirm(`Mitglied „${member.userId}“ aus dem Workspace entfernen?`)) {
    return
  }
  error.value = null
  const before = await runHook('workspaces.member.before-remove', {
    workspaceKey: props.workspaceKey,
    userId: member.userId,
  })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Entfernen abgebrochen.'
    return
  }
  busyUserId.value = member.userId
  try {
    await api.removeMember(props.workspaceKey, member.userId)
    await runHook('workspaces.member.after-remove', { workspaceKey: props.workspaceKey, userId: member.userId })
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyUserId.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.members {
  margin-top: calc(var(--cal-space) * 3);
}

.members h2 {
  font-size: 1.1em;
  margin-bottom: var(--cal-space);
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

.sub {
  display: block;
  font-size: 0.8em;
  color: var(--cal-color-muted);
}

.mono {
  font-family: var(--cal-font-mono, monospace);
}

.actions {
  display: flex;
  gap: calc(var(--cal-space) * 1.5);
  align-items: center;
}

.link-danger {
  background: none;
  border: 0;
  color: var(--cal-color-danger);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.more {
  margin-top: var(--cal-space);
}

.link {
  background: none;
  border: 0;
  color: var(--cal-color-accent);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.add {
  display: flex;
  gap: var(--cal-space);
  margin-top: calc(var(--cal-space) * 1.5);
}

.add-input {
  flex: 1;
  max-width: 220px;
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
