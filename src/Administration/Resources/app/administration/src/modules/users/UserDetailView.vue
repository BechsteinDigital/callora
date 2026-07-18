<template>
  <section class="detail">
    <h1>{{ isEdit ? 'Benutzer bearbeiten' : 'Benutzer anlegen' }}</h1>

    <p v-if="error" class="error">{{ error }}</p>

    <form class="form" @submit.prevent="save">
      <label>Login
        <BaseInput v-model="externalId" name="externalId" :disabled="isEdit" />
      </label>
      <label>E-Mail
        <BaseInput v-model="email" type="email" name="email" />
      </label>
      <label>Anzeigename
        <BaseInput v-model="displayName" name="displayName" />
      </label>
      <label>
        Passwort
        <span v-if="isEdit" class="hint">(leer lassen, um es beizubehalten)</span>
        <BaseInput v-model="password" type="password" name="password" />
      </label>
      <label v-if="canAssignRole">Rolle
        <select v-model="role" name="role" class="select">
          <option value="">— keine —</option>
          <option v-for="r in roles" :key="r.role" :value="r.role">{{ r.role }}</option>
        </select>
      </label>

      <div class="buttons">
        <BaseButton type="submit" :disabled="saving">{{ isEdit ? 'Speichern' : 'Anlegen' }}</BaseButton>
        <RouterLink class="cancel" to="/users">Abbrechen</RouterLink>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { usersApi, type Role } from './usersApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import BaseInput from '@/core/ui/BaseInput.vue'

const route = useRoute()
const router = useRouter()
const userId = computed(() => (route.params.userId as string | undefined) ?? null)
const isEdit = computed(() => userId.value !== null)

const externalId = ref('')
const email = ref('')
const displayName = ref('')
const password = ref('')
const role = ref('')
const initialRole = ref('')
const roles = ref<Role[]>([])
const error = ref<string | null>(null)
const saving = ref(false)

const ctx = useAuthStore().context
// The role picker both reads the current role (GET endpoints gated on role.read)
// and changes it (PUT gated on role.update), so it needs both — consistent with
// the list view's read path. role.update implies role.read in any sensible role.
const canAssignRole = computed(
  () => hasPermission(ctx.value, 'role.read') && hasPermission(ctx.value, 'role.update'),
)

async function load(): Promise<void> {
  try {
    if (canAssignRole.value) {
      roles.value = await usersApi.listRoles()
    }
    if (isEdit.value && userId.value) {
      const user = await usersApi.get(userId.value)
      externalId.value = user.externalId
      email.value = user.email ?? ''
      displayName.value = user.displayName ?? ''
      if (canAssignRole.value) {
        const assignments = await usersApi.listRoleAssignments()
        initialRole.value = assignments[userId.value] ?? ''
        role.value = initialRole.value
      }
    }
  } catch (e) {
    error.value = (e as Error).message
  }
}

async function save(): Promise<void> {
  saving.value = true
  error.value = null
  try {
    const id = isEdit.value && userId.value ? userId.value : externalId.value
    if (isEdit.value) {
      await usersApi.update(id, {
        email: email.value || null,
        displayName: displayName.value || null,
        // Empty stays null so the backend keeps the current password.
        password: password.value || null,
      })
    } else {
      await usersApi.create({
        externalId: externalId.value,
        email: email.value || null,
        displayName: displayName.value || null,
        password: password.value,
      })
    }
    // Assigning a role needs role.update; only send it when it actually changed.
    if (canAssignRole.value && role.value && role.value !== initialRole.value) {
      await usersApi.assignRole(id, role.value)
    }
    router.push('/users')
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
  max-width: 420px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: calc(var(--cal-space) * 1.5);
  margin-top: calc(var(--cal-space) * 2);
}

.form label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
}

.hint {
  font-size: 0.85em;
}

.select {
  width: 100%;
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.buttons {
  display: flex;
  align-items: center;
  gap: calc(var(--cal-space) * 2);
  margin-top: var(--cal-space);
}

.cancel {
  color: var(--cal-color-muted);
  text-decoration: none;
}

.error {
  color: var(--cal-color-danger);
}
</style>
