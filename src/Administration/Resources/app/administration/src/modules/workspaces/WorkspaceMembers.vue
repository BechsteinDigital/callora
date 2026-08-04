<template>
  <div class="members">
    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="members"
        row-key="userId"
        :loading="loading"
        :error="error"
        :empty-icon="Users"
        empty-title="Keine Mitglieder."
        empty-description="Weisen Sie unten einen Benutzer mit einer Rolle zu."
      >
        <template #cell-displayName="{ row }">
          <span class="members__name">
            {{ row.displayName || row.userId }}
            <span v-if="row.displayName" class="members__id">{{ row.userId }}</span>
          </span>
        </template>

        <template #cell-role="{ row }">
          <CalBadge tone="neutral">{{ row.role }}</CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="members__actions">
            <CalButton
              v-if="canManage"
              variant="danger-ghost"
              size="sm"
              :disabled="busyUserId === row.userId"
              @click="remove(row)"
            >
              Entfernen
            </CalButton>
            <ExtensionSlot name="workspaces.members.row-actions" :ctx="row" />
          </div>
        </template>
      </CalDataTable>

      <template v-if="!loading && nextCursor" #footer>
        <CalButton :loading="loadingMore" @click="loadMore">
          Mehr laden ({{ members.length }}{{ total ? ` von ${total}` : '' }})
        </CalButton>
      </template>
    </CalCard>

    <CalCard v-if="canManage" class="members__add" title="Mitglied zuweisen">
      <form class="members__form" @submit.prevent="add">
        <CalField v-slot="{ id }" label="Benutzer-Login">
          <CalInput :id="id" v-model="userId" name="memberUserId" />
        </CalField>
        <CalField v-slot="{ id }" label="Rolle">
          <CalInput :id="id" v-model="role" name="memberRole" />
        </CalField>
        <CalButton
          type="submit"
          variant="primary"
          :loading="adding"
          :disabled="!userId.trim() || !role.trim()"
        >
          Zuweisen
        </CalButton>
      </form>
    </CalCard>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Users } from 'lucide-vue-next'
import { workspacesApi, type WorkspaceMember } from './workspacesApi'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

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

const columns: readonly DataTableColumn[] = [
  { key: 'displayName', label: 'Benutzer' },
  { key: 'role', label: 'Rolle', width: '200px' },
  { key: 'actions', label: '', align: 'end', width: '140px' },
]

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
    toast.success(`„${id}“ als „${roleName}“ zugewiesen.`)
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
  const confirmed = await confirm({
    title: `Mitglied „${member.userId}“ entfernen?`,
    description: 'Der Zugriff auf diesen Workspace endet sofort. Das Konto selbst bleibt bestehen.',
    confirmLabel: 'Entfernen',
    tone: 'danger',
  })
  if (!confirmed) {
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
    toast.success(`„${member.userId}“ entfernt.`)
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
.members__name {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.members__id {
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
  font-weight: var(--cal-weight-normal);
  color: var(--cal-text-muted);
}

.members__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}

.members__add {
  margin-top: var(--cal-space-4);
}

.members__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.members__form > :deep(.cal-field) {
  flex: 1;
  min-width: 200px;
}
</style>
