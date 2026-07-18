<template>
  <section class="detail">
    <h1>{{ isEdit ? 'Rolle bearbeiten' : 'Rolle anlegen' }}</h1>
    <p v-if="error" class="error">{{ error }}</p>

    <form class="form" @submit.prevent="save">
      <label>Name
        <BaseInput v-model="roleName" name="role" :disabled="isEdit" />
      </label>

      <fieldset class="perms">
        <legend>Rechte</legend>
        <div v-for="group in grouped" :key="group.function" class="group">
          <h3>{{ group.function }}</h3>
          <label v-for="p in group.permissions" :key="p.permissionKey" class="perm">
            <input type="checkbox" :value="p.permissionKey" v-model="selected" />
            {{ p.action }}
          </label>
        </div>
        <p v-if="!grouped.length" class="hint">Keine Rechte verfügbar.</p>
      </fieldset>

      <div class="buttons">
        <BaseButton type="submit" :disabled="saving">{{ isEdit ? 'Speichern' : 'Anlegen' }}</BaseButton>
        <RouterLink class="cancel" to="/roles">Abbrechen</RouterLink>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { rolesApi, type Permission } from './rolesApi'
import BaseButton from '@/core/ui/BaseButton.vue'
import BaseInput from '@/core/ui/BaseInput.vue'

const route = useRoute()
const router = useRouter()
const editRole = computed(() => (route.params.role as string | undefined) ?? null)
const isEdit = computed(() => editRole.value !== null)

const roleName = ref('')
const permissions = ref<Permission[]>([])
const selected = ref<string[]>([])
const error = ref<string | null>(null)
const saving = ref(false)

// All available permissions grouped by their function for the checkbox matrix.
const grouped = computed(() => {
  const map = new Map<string, Permission[]>()
  for (const p of permissions.value) {
    const list = map.get(p.function) ?? []
    list.push(p)
    map.set(p.function, list)
  }
  return [...map.entries()].map(([fn, perms]) => ({ function: fn, permissions: perms }))
})

async function load(): Promise<void> {
  try {
    permissions.value = await rolesApi.listPermissions()
    if (isEdit.value && editRole.value) {
      roleName.value = editRole.value
      const roles = await rolesApi.list()
      const current = roles.find((r) => r.role === editRole.value)
      // Drop the "*" wildcard — it is not a selectable concrete permission.
      selected.value = current ? current.permissions.filter((p) => p !== '*') : []
    }
  } catch (e) {
    error.value = (e as Error).message
  }
}

async function save(): Promise<void> {
  const name = isEdit.value && editRole.value ? editRole.value : roleName.value.trim()
  if (!name) {
    error.value = 'Name ist erforderlich.'
    return
  }
  saving.value = true
  error.value = null
  try {
    await rolesApi.upsert(name, selected.value)
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
.detail {
  padding: calc(var(--cal-space) * 3);
  max-width: 560px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: calc(var(--cal-space) * 2);
  margin-top: calc(var(--cal-space) * 2);
}

.form > label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
  max-width: 320px;
}

.perms {
  border: 1px solid var(--cal-color-surface);
  border-radius: var(--cal-radius);
  padding: calc(var(--cal-space) * 1.5);
}

.perms legend {
  color: var(--cal-color-muted);
  padding: 0 var(--cal-space);
}

.group {
  margin-bottom: calc(var(--cal-space) * 1.5);
}

.group h3 {
  margin: 0 0 var(--cal-space);
  font-size: 0.9em;
  text-transform: capitalize;
}

.perm {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-right: calc(var(--cal-space) * 2);
  color: var(--cal-color-text);
}

.buttons {
  display: flex;
  align-items: center;
  gap: calc(var(--cal-space) * 2);
}

.cancel {
  color: var(--cal-color-muted);
  text-decoration: none;
}

.hint,
.error {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
