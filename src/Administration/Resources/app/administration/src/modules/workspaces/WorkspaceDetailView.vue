<template>
  <CalPage narrow>
    <CalPageHeader
      :title="isEdit ? (loaded?.displayName ?? 'Workspace bearbeiten') : 'Workspace anlegen'"
      :description="isEdit ? 'Stammdaten, Mitglieder, Plugins und Surfaces dieses Arbeitsbereichs.' : undefined"
      back-to="/workspaces"
      back-label="Alle Workspaces"
    >
      <template v-if="isEdit && loaded" #title-suffix>
        <CalBadge :tone="loaded.isActive ? 'success' : 'neutral'" dot>
          {{ loaded.isActive ? 'Aktiv' : 'Inaktiv' }}
        </CalBadge>
      </template>
    </CalPageHeader>

    <CalAlert v-if="error" class="detail__error" tone="danger">{{ error }}</CalAlert>

    <form @submit.prevent="save">
      <CalCard title="Stammdaten">
        <div class="detail__fields">
          <CalField v-slot="{ id }" label="Schlüssel" required :description="isEdit ? 'Nicht änderbar.' : undefined">
            <CalInput :id="id" v-model="workspaceKey" name="workspaceKey" :disabled="isEdit" />
          </CalField>

          <CalField v-slot="{ id }" label="Anzeigename" required>
            <CalInput :id="id" v-model="displayName" name="displayName" />
          </CalField>

          <CalField v-slot="{ id }" label="Typ" required>
            <CalInput :id="id" v-model="workspaceType" name="workspaceType" />
          </CalField>

          <CalField
            v-slot="{ id }"
            label="Öffentliche Basis-URL"
            hint="optional"
            :description="isEdit
              ? 'Wird im Reiter „Surfaces“ je Zugang gepflegt.'
              : 'Richtet den Standard-Zugang („default“-Surface) ein. Weitere Zugänge legen Sie danach unter „Surfaces“ an.'"
          >
            <CalInput
              :id="id"
              v-model="defaultSurfaceBaseUrl"
              type="url"
              name="defaultSurfaceBaseUrl"
              :icon="Globe"
              :disabled="isEdit"
            />
          </CalField>

          <CalField label="Zustand">
            <CalSwitch v-model="isActive" name="isActive">Aktiv</CalSwitch>
          </CalField>

          <ExtensionSlot name="workspaces.detail.fields" :ctx="{ workspaceKey: workspaceKey || null }" />
        </div>

        <CalDescriptionList v-if="isEdit && loaded" class="detail__meta" :items="metaItems" />

        <template #footer>
          <div class="buttons">
            <CalButton variant="ghost" to="/workspaces">Abbrechen</CalButton>
            <CalButton type="submit" variant="primary" :loading="saving" :disabled="!canSubmit">
              {{ isEdit ? 'Speichern' : 'Anlegen' }}
            </CalButton>
          </div>
        </template>
      </CalCard>
    </form>

    <!-- The three related lists are tabbed rather than stacked: they are peers,
         and stacking them buried Surfaces below two long tables. -->
    <CalTabs v-if="isEdit && loaded" v-model="activeTab" class="detail__tabs" :tabs="tabs">
      <template #members>
        <WorkspaceMembers :workspace-key="loaded.workspaceKey" :can-manage="canManage" />
      </template>
      <template #plugins>
        <WorkspacePlugins :workspace-key="loaded.workspaceKey" :can-manage="canManagePlugins" />
      </template>
      <template #surfaces>
        <WorkspaceSurfaces :workspace-key="loaded.workspaceKey" :can-manage="canManage" />
      </template>
    </CalTabs>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Globe, Layers, Puzzle, Users } from 'lucide-vue-next'
import { workspacesApi, type Workspace } from './workspacesApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import WorkspaceMembers from './WorkspaceMembers.vue'
import WorkspacePlugins from './WorkspacePlugins.vue'
import WorkspaceSurfaces from './WorkspaceSurfaces.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDescriptionList from '@/core/ui/CalDescriptionList.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSwitch from '@/core/ui/CalSwitch.vue'
import CalTabs from '@/core/ui/CalTabs.vue'
import type { DescriptionItem } from '@/core/ui/descriptionList'
import type { TabItem } from '@/core/ui/tabs'
import { toast } from '@/core/feedback/toasts'

const route = useRoute()
const router = useRouter()
const routeKey = computed(() => (route.params.workspaceKey as string | undefined) ?? null)
const isEdit = computed(() => routeKey.value !== null)

const workspaceKey = ref('')
const displayName = ref('')
const workspaceType = ref('')
const defaultSurfaceBaseUrl = ref('')
const isActive = ref(true)
const loaded = ref<Workspace | null>(null)
const error = ref<string | null>(null)
const saving = ref(false)
const activeTab = ref('members')

const tabs: readonly TabItem[] = [
  { value: 'members', label: 'Mitglieder', icon: Users },
  { value: 'plugins', label: 'Plugins', icon: Puzzle },
  { value: 'surfaces', label: 'Surfaces', icon: Layers },
]

// Member add/remove goes through the PUT/DELETE member routes, gated on
// workspace.update — same key that guards the workspace upsert. UI-only.
const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'workspace.update'))
const canManagePlugins = computed(() => hasPermission(ctx.value, 'plugin.execute'))

// The key and display name are required; the type is required by the backend too.
const canSubmit = computed(
  () => workspaceKey.value.trim() !== '' && displayName.value.trim() !== '' && workspaceType.value.trim() !== '',
)

const metaItems = computed<DescriptionItem[]>(() => {
  const workspace = loaded.value
  if (!workspace) {
    return []
  }
  const items: DescriptionItem[] = [{ term: 'Mandant', value: workspace.tenantKey, mono: true }]
  return items
})

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
  defaultSurfaceBaseUrl: string | null
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
    defaultSurfaceBaseUrl: defaultSurfaceBaseUrl.value || null,
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
      defaultSurfaceBaseUrl: draft.defaultSurfaceBaseUrl,
    })
    await runHook('workspaces.after-save', { workspaceKey: key })
    toast.success(isEdit.value ? `Workspace „${key}“ gespeichert.` : `Workspace „${key}“ angelegt.`)
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
.detail__error {
  margin-bottom: var(--cal-space-4);
}

.detail__fields {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-5);
}

.detail__meta {
  margin-top: var(--cal-space-5);
  padding-top: var(--cal-space-4);
  border-top: 1px solid var(--cal-border-subtle);
}

.detail__tabs {
  margin-top: var(--cal-space-6);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
