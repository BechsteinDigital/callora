<template>
  <section class="detail">
    <h1>{{ isEdit ? 'Workspace bearbeiten' : 'Workspace anlegen' }}</h1>

    <p v-if="error" class="error">{{ error }}</p>

    <form class="form" @submit.prevent="save">
      <label>Schlüssel
        <BaseInput v-model="workspaceKey" name="workspaceKey" :disabled="isEdit" />
      </label>
      <label>Anzeigename
        <BaseInput v-model="displayName" name="displayName" />
      </label>
      <label>Typ
        <BaseInput v-model="workspaceType" name="workspaceType" />
      </label>
      <label>Öffentliche Basis-URL
        <span class="hint">(optional)</span>
        <BaseInput v-model="publicBaseUrl" type="url" name="publicBaseUrl" />
      </label>
      <label class="check">
        <input type="checkbox" v-model="isActive" name="isActive" />
        Aktiv
      </label>

      <ExtensionSlot name="workspaces.detail.fields" :ctx="{ workspaceKey: workspaceKey || null }" />

      <dl v-if="isEdit && loaded" class="meta">
        <div><dt>Tenant</dt><dd class="mono">{{ loaded.tenantKey }}</dd></div>
        <div v-if="loaded.publicHost"><dt>Öffentlicher Host</dt><dd class="mono">{{ loaded.publicHost }}</dd></div>
        <div><dt>Pfad-Präfix</dt><dd class="mono">{{ loaded.publicPathPrefix }}</dd></div>
      </dl>

      <div class="buttons">
        <BaseButton type="submit" :disabled="saving || !canSubmit">{{ isEdit ? 'Speichern' : 'Anlegen' }}</BaseButton>
        <RouterLink class="cancel" to="/workspaces">Abbrechen</RouterLink>
      </div>
    </form>

    <WorkspaceMembers v-if="isEdit && loaded" :workspace-key="loaded.workspaceKey" :can-manage="canManage" />
    <WorkspacePlugins
      v-if="isEdit && loaded"
      :workspace-key="loaded.workspaceKey"
      :can-manage="canManagePlugins"
    />
    <WorkspaceSurfaces v-if="isEdit && loaded" :workspace-key="loaded.workspaceKey" :can-manage="canManage" />
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { workspacesApi, type Workspace } from './workspacesApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import BaseInput from '@/core/ui/BaseInput.vue'
import WorkspaceMembers from './WorkspaceMembers.vue'
import WorkspacePlugins from './WorkspacePlugins.vue'
import WorkspaceSurfaces from './WorkspaceSurfaces.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const route = useRoute()
const router = useRouter()
const routeKey = computed(() => (route.params.workspaceKey as string | undefined) ?? null)
const isEdit = computed(() => routeKey.value !== null)

const workspaceKey = ref('')
const displayName = ref('')
const workspaceType = ref('')
const publicBaseUrl = ref('')
const isActive = ref(true)
const loaded = ref<Workspace | null>(null)
const error = ref<string | null>(null)
const saving = ref(false)

// Member add/remove goes through the PUT/DELETE member routes, gated on
// workspace.update — same key that guards the workspace upsert. UI-only.
const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'workspace.update'))
const canManagePlugins = computed(() => hasPermission(ctx.value, 'plugin.execute'))

// The key and display name are required; the type is required by the backend too.
const canSubmit = computed(
  () => workspaceKey.value.trim() !== '' && displayName.value.trim() !== '' && workspaceType.value.trim() !== '',
)

// Resolve the workspaces service through the override registry: a plugin may replace it.
const api = useService('workspacesApi', workspacesApi)

async function load(): Promise<void> {
  if (!isEdit.value || !routeKey.value) {
    return
  }
  try {
    const workspace = await api.get(routeKey.value)
    loaded.value = workspace
    workspaceKey.value = workspace.workspaceKey
    displayName.value = workspace.displayName
    workspaceType.value = workspace.workspaceType
    publicBaseUrl.value = workspace.publicBaseUrl ?? ''
    isActive.value = workspace.isActive
  } catch (e) {
    error.value = (e as Error).message
  }
}

// A before-save hook may enrich the mutable fields or veto; the key and mode are
// read-only context (a plugin does not rewrite which workspace is being saved).
interface WorkspaceSaveDraft {
  readonly workspaceKey: string
  readonly isEdit: boolean
  displayName: string
  workspaceType: string
  isActive: boolean
  publicBaseUrl: string | null
}

async function save(): Promise<void> {
  // Guard the submit path directly (Enter key / form submit bypasses the disabled button).
  if (!canSubmit.value) {
    return
  }
  // On edit the route param is the canonical key (the input is display-only there);
  // on create it comes from the form. Mirrors UserDetailView's identity handling.
  const key = isEdit.value && routeKey.value ? routeKey.value : workspaceKey.value.trim()
  const draft: WorkspaceSaveDraft = {
    workspaceKey: key,
    isEdit: isEdit.value,
    displayName: displayName.value,
    workspaceType: workspaceType.value,
    isActive: isActive.value,
    publicBaseUrl: publicBaseUrl.value || null,
  }
  const before = await runHook('workspaces.before-save', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }

  saving.value = true
  error.value = null
  try {
    await api.upsert(key, {
      displayName: draft.displayName,
      workspaceType: draft.workspaceType,
      isActive: draft.isActive,
      publicBaseUrl: draft.publicBaseUrl,
    })
    await runHook('workspaces.after-save', { workspaceKey: key })
    router.push('/workspaces')
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
  max-width: 920px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: calc(var(--cal-space) * 1.5);
  margin-top: calc(var(--cal-space) * 2);
  max-width: 460px;
}

.form label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
}

.form label.check {
  flex-direction: row;
  align-items: center;
  gap: var(--cal-space);
}

.hint {
  font-size: 0.85em;
}

.meta {
  margin: 0;
  padding: var(--cal-space) 0 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.meta div {
  display: flex;
  justify-content: space-between;
  gap: var(--cal-space);
}

.meta dt {
  color: var(--cal-color-muted);
}

.meta dd {
  margin: 0;
}

.mono {
  font-family: var(--cal-font-mono, monospace);
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
