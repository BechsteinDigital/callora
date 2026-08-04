<template>
  <CalPage narrow>
    <CalPageHeader
      :title="isEdit ? 'Rolle bearbeiten' : 'Rolle anlegen'"
      description="Wählen Sie die Rechte, die diese Rolle gewährt."
      back-to="/roles"
      back-label="Alle Rollen"
    />

    <CalAlert v-if="error" class="detail__error" tone="danger">{{ error }}</CalAlert>

    <form @submit.prevent="save">
      <CalCard title="Rolle">
        <CalField v-slot="{ id }" label="Name" required :description="isEdit ? 'Nicht änderbar.' : undefined">
          <CalInput :id="id" v-model="roleName" name="role" :disabled="isEdit" />
        </CalField>
      </CalCard>

      <CalCard
        class="detail__perms"
        title="Rechte"
        :description="`${selected.length} von ${permissions.length} ausgewählt`"
      >
        <div v-if="grouped.length" class="perms">
          <section v-for="group in grouped" :key="group.function" class="perms__group">
            <div class="perms__head">
              <h3 class="perms__title">{{ group.function }}</h3>
              <CalButton variant="ghost" size="sm" @click="toggleGroup(group)">
                {{ isGroupFull(group) ? 'Keine' : 'Alle' }}
              </CalButton>
            </div>
            <div class="perms__items">
              <CalCheckbox
                v-for="p in group.permissions"
                :key="p.permissionKey"
                :model-value="selected.includes(p.permissionKey)"
                @update:model-value="togglePermission(p.permissionKey, $event)"
              >
                {{ p.action }}
              </CalCheckbox>
            </div>
          </section>
        </div>
        <CalEmptyState v-else compact title="Keine Rechte verfügbar." />

        <ExtensionSlot name="roles.detail.fields" :ctx="{ role: editRole ?? roleName }" />

        <template #footer>
          <div class="buttons">
            <CalButton variant="ghost" to="/roles">Abbrechen</CalButton>
            <CalButton type="submit" variant="primary" :loading="saving">
              {{ isEdit ? 'Speichern' : 'Anlegen' }}
            </CalButton>
          </div>
        </template>
      </CalCard>
    </form>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { rolesApi, type Permission } from './rolesApi'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalCheckbox from '@/core/ui/CalCheckbox.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import type { PermissionGroup } from './permissionGroup'
import { toast } from '@/core/feedback/toasts'

const route = useRoute()
const router = useRouter()
const editRole = computed(() => (route.params.role as string | undefined) ?? null)
const isEdit = computed(() => editRole.value !== null)

const roleName = ref('')
const permissions = ref<Permission[]>([])
const selected = ref<string[]>([])
const error = ref<string | null>(null)
const saving = ref(false)

// Resolve the roles service through the override registry: a plugin may replace it.
const api = useService('rolesApi', rolesApi)

// All available permissions grouped by their function for the checkbox matrix.
const grouped = computed<PermissionGroup[]>(() => {
  const map = new Map<string, Permission[]>()
  for (const p of permissions.value) {
    const list = map.get(p.function) ?? []
    list.push(p)
    map.set(p.function, list)
  }
  return [...map.entries()].map(([fn, perms]) => ({ function: fn, permissions: perms }))
})

function togglePermission(key: string, checked: boolean): void {
  selected.value = checked ? [...selected.value, key] : selected.value.filter((k) => k !== key)
}

function isGroupFull(group: PermissionGroup): boolean {
  return group.permissions.every((p) => selected.value.includes(p.permissionKey))
}

// Whole-function toggling: a role for one subsystem usually wants all of its
// actions, and ticking six boxes by hand is where mistakes creep in.
function toggleGroup(group: PermissionGroup): void {
  const keys = group.permissions.map((p) => p.permissionKey)
  if (isGroupFull(group)) {
    selected.value = selected.value.filter((k) => !keys.includes(k))
    return
  }
  selected.value = [...new Set([...selected.value, ...keys])]
}

async function load(): Promise<void> {
  try {
    permissions.value = await api.listPermissions()
    if (isEdit.value && editRole.value) {
      roleName.value = editRole.value
      const roles = await api.list()
      const current = roles.find((r) => r.role === editRole.value)
      // Drop the "*" wildcard — it is not a selectable concrete permission.
      selected.value = current ? current.permissions.filter((p) => p !== '*') : []
    }
  } catch (e) {
    error.value = (e as Error).message
  }
}

// A before-save hook may adjust the permission set or veto; the role name is
// read-only context.
interface RoleSaveDraft {
  readonly role: string
  permissions: string[]
}

async function save(): Promise<void> {
  const name = isEdit.value && editRole.value ? editRole.value : roleName.value.trim()
  if (!name) {
    error.value = 'Name ist erforderlich.'
    return
  }
  const draft: RoleSaveDraft = { role: name, permissions: [...selected.value] }
  const before = await runHook('roles.before-save', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }

  saving.value = true
  error.value = null
  try {
    await api.upsert(name, draft.permissions)
    await runHook('roles.after-save', { role: name })
    toast.success(isEdit.value ? `Rolle „${name}“ gespeichert.` : `Rolle „${name}“ angelegt.`)
    router.push('/roles')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.detail__error {
  margin-bottom: var(--cal-space-4);
}

.detail__perms {
  margin-top: var(--cal-space-4);
}

.perms {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-5);
}

.perms__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cal-space-2);
  margin-bottom: var(--cal-space-2);
  padding-bottom: var(--cal-space-1);
  border-bottom: 1px solid var(--cal-border-subtle);
}

.perms__title {
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-semibold);
  text-transform: capitalize;
}

.perms__items {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cal-space-2) var(--cal-space-5);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
