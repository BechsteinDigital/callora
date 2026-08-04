<template>
  <CalPage narrow>
    <CalPageHeader
      :title="isEdit ? 'Benutzer bearbeiten' : 'Benutzer anlegen'"
      :description="isEdit ? 'Stammdaten, Passwort und Rollenzuweisung dieses Kontos.' : undefined"
      back-to="/users"
      back-label="Alle Benutzer"
    />

    <CalAlert v-if="error" class="detail__error" tone="danger">{{ error }}</CalAlert>

    <form @submit.prevent="save">
      <CalCard title="Konto">
        <div class="detail__fields">
          <CalField v-slot="{ id }" label="Login" :description="isEdit ? 'Nicht änderbar.' : undefined" required>
            <CalInput :id="id" v-model="externalId" name="externalId" :disabled="isEdit" />
          </CalField>

          <CalField v-slot="{ id }" label="E-Mail">
            <CalInput :id="id" v-model="email" type="email" name="email" />
          </CalField>

          <CalField v-slot="{ id }" label="Anzeigename">
            <CalInput :id="id" v-model="displayName" name="displayName" />
          </CalField>

          <CalField
            v-slot="{ id }"
            label="Passwort"
            :hint="isEdit ? 'optional' : undefined"
            :description="isEdit ? 'Leer lassen, um das bestehende Passwort beizubehalten.' : undefined"
          >
            <CalInput
              :id="id"
              v-model="password"
              type="password"
              name="password"
              autocomplete="new-password"
              :icon="KeyRound"
            />
          </CalField>

          <CalField v-if="canAssignRole" v-slot="{ id }" label="Rolle">
            <CalSelect :id="id" v-model="role" name="role">
              <option value="">— keine —</option>
              <option v-for="r in roles" :key="r.role" :value="r.role">{{ r.role }}</option>
            </CalSelect>
          </CalField>

          <ExtensionSlot name="users.detail.fields" :ctx="{ userId: userId ?? externalId }" />
        </div>

        <template #footer>
          <div class="buttons">
            <CalButton variant="ghost" to="/users">Abbrechen</CalButton>
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
import { KeyRound } from 'lucide-vue-next'
import { usersApi, type Role } from './usersApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import { toast } from '@/core/feedback/toasts'

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

// Resolve the user service through the override registry: a plugin may replace it.
const api = useService('usersApi', usersApi)

async function load(): Promise<void> {
  try {
    if (canAssignRole.value) {
      roles.value = await api.listRoles()
    }
    if (isEdit.value && userId.value) {
      const user = await api.get(userId.value)
      externalId.value = user.externalId
      email.value = user.email ?? ''
      displayName.value = user.displayName ?? ''
      if (canAssignRole.value) {
        const assignments = await api.listRoleAssignments()
        initialRole.value = assignments[userId.value] ?? ''
        role.value = initialRole.value
      }
    }
  } catch (e) {
    error.value = (e as Error).message
  }
}

// A before-save hook may enrich the mutable fields or veto; identity and mode are
// read-only context (a plugin does not rewrite who is being saved).
interface UserSaveDraft {
  readonly externalId: string
  readonly isEdit: boolean
  email: string | null
  displayName: string | null
  role: string | null
}

async function save(): Promise<void> {
  const id = isEdit.value && userId.value ? userId.value : externalId.value
  const draft: UserSaveDraft = {
    externalId: id,
    isEdit: isEdit.value,
    email: email.value || null,
    displayName: displayName.value || null,
    role: canAssignRole.value ? role.value : null,
  }
  const before = await runHook('users.before-save', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }

  saving.value = true
  error.value = null
  try {
    if (isEdit.value) {
      await api.update(id, {
        email: draft.email,
        displayName: draft.displayName,
        // Empty stays null so the backend keeps the current password.
        password: password.value || null,
      })
    } else {
      await api.create({
        externalId: id,
        email: draft.email,
        displayName: draft.displayName,
        password: password.value,
      })
    }
    // Assigning a role needs role.update; only send it when it actually changed.
    if (canAssignRole.value && draft.role && draft.role !== initialRole.value) {
      await api.assignRole(id, draft.role)
    }
    await runHook('users.after-save', { userId: id })
    toast.success(isEdit.value ? `Benutzer „${id}“ gespeichert.` : `Benutzer „${id}“ angelegt.`)
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
.detail__error {
  margin-bottom: var(--cal-space-4);
}

.detail__fields {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-5);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
