<template>
  <div class="surfaces">
    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="surfaces"
        row-key="surfaceKey"
        :loading="loading"
        :error="error"
        :empty-icon="Layers"
        empty-title="Keine Surfaces."
        empty-description="Der öffentliche Zugang läuft über die default-Surface."
      >
        <template #cell-location="{ row }">
          <span class="surfaces__location">{{ row.publicHost || '—' }}{{ row.publicPathPrefix }}</span>
        </template>

        <template #cell-theme="{ row }">
          <CalBadge :tone="row.themePluginId ? 'accent' : 'neutral'" variant="outline">
            {{ row.themePluginId ? row.themePluginId : 'vom Workspace' }}
          </CalBadge>
        </template>

        <template #cell-accessMode="{ row }">
          <CalBadge :tone="row.accessMode === 'Public' ? 'warning' : 'neutral'">{{ row.accessMode }}</CalBadge>
        </template>

        <template #cell-isActive="{ row }">
          <CalBadge :tone="row.isActive ? 'success' : 'neutral'" dot>
            {{ row.isActive ? 'Aktiv' : 'Inaktiv' }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="surfaces__actions">
            <CalButton variant="ghost" size="sm" :icon="Palette" @click="openTheme(row)">Design</CalButton>
            <CalButton
              v-if="canManage"
              variant="ghost"
              size="sm"
              :disabled="busyKey === row.surfaceKey"
              @click="startEdit(row)"
            >
              Bearbeiten
            </CalButton>
            <CalButton
              v-if="canManage"
              variant="danger-ghost"
              size="sm"
              :disabled="busyKey === row.surfaceKey"
              @click="remove(row)"
            >
              Löschen
            </CalButton>
            <ExtensionSlot name="workspaces.surfaces.row-actions" :ctx="row" />
          </div>
        </template>
      </CalDataTable>
    </CalCard>

    <CalCard
      v-if="canManage"
      class="surfaces__editor"
      :title="editingKey ? `Surface „${editingKey}“ bearbeiten` : 'Surface anlegen'"
      description="Eine Surface ist ein Zugang zum Workspace — eigener Host, Pfad und Zugangsmodus."
    >
      <form class="surfaces__form" @submit.prevent="save">
        <CalField v-slot="{ id }" label="Schlüssel" required :description="editingKey ? 'Nicht änderbar.' : undefined">
          <CalInput :id="id" v-model="formKey" name="surfaceKey" :disabled="editingKey !== null" />
        </CalField>
        <CalField v-slot="{ id }" label="Anzeigename" required>
          <CalInput :id="id" v-model="formDisplayName" name="surfaceDisplayName" />
        </CalField>
        <CalField v-slot="{ id }" label="Typ" required>
          <CalInput :id="id" v-model="formType" name="surfaceType" />
        </CalField>
        <CalField v-slot="{ id }" label="Zugang">
          <CalSelect :id="id" v-model="formAccessMode" name="surfaceAccessMode">
            <option v-for="mode in accessModes" :key="mode" :value="mode">{{ mode }}</option>
          </CalSelect>
        </CalField>
        <CalField v-slot="{ id }" label="Öffentlicher Host" hint="optional">
          <CalInput :id="id" v-model="formHost" name="surfaceHost" />
        </CalField>
        <CalField v-slot="{ id }" label="Pfad-Präfix" required>
          <CalInput :id="id" v-model="formPathPrefix" name="surfacePathPrefix" />
        </CalField>
        <CalField v-slot="{ id }" label="Basis-URL" hint="optional">
          <CalInput :id="id" v-model="formBaseUrl" type="url" name="surfaceBaseUrl" />
        </CalField>
        <CalField v-slot="{ id }" label="Locale" hint="optional">
          <CalInput :id="id" v-model="formLocale" name="surfaceLocale" />
        </CalField>
        <CalField label="Zustand">
          <CalSwitch v-model="formActive" name="surfaceActive">Aktiv</CalSwitch>
        </CalField>
      </form>

      <template #footer>
        <div class="buttons">
          <CalButton v-if="editingKey" variant="ghost" @click="resetForm">Abbrechen</CalButton>
          <CalButton variant="primary" :loading="saving" :disabled="!canSubmit" @click="save">
            {{ editingKey ? 'Speichern' : 'Anlegen' }}
          </CalButton>
        </div>
      </template>
    </CalCard>

    <SurfaceThemeDialog
      v-if="themeSurfaceKey"
      v-model:open="themeDialogOpen"
      :workspace-key="workspaceKey"
      :surface-key="themeSurfaceKey"
      :can-manage="canManage"
      @changed="load"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Layers, Palette } from 'lucide-vue-next'
import { workspacesApi, SURFACE_ACCESS_MODES, type WorkspaceSurface } from './workspacesApi'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import CalSwitch from '@/core/ui/CalSwitch.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'
import SurfaceThemeDialog from '@/modules/themes/SurfaceThemeDialog.vue'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

const props = defineProps<{ workspaceKey: string; canManage: boolean }>()

const accessModes = SURFACE_ACCESS_MODES

const surfaces = ref<WorkspaceSurface[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyKey = ref<string | null>(null)
const saving = ref(false)

// The design editor is a dialog rather than a route: it belongs to one row and
// the operator returns to the list right after.
const themeDialogOpen = ref(false)
const themeSurfaceKey = ref<string | null>(null)

function openTheme(surface: WorkspaceSurface): void {
  themeSurfaceKey.value = surface.surfaceKey
  themeDialogOpen.value = true
}

const columns: readonly DataTableColumn[] = [
  { key: 'surfaceKey', label: 'Schlüssel', mono: true },
  { key: 'displayName', label: 'Name' },
  { key: 'surfaceType', label: 'Typ', width: '100px' },
  { key: 'accessMode', label: 'Zugang', width: '140px' },
  { key: 'location', label: 'Host / Pfad' },
  { key: 'theme', label: 'Design', width: '170px' },
  { key: 'isActive', label: 'Aktiv', width: '110px' },
  { key: 'actions', label: '', align: 'end', width: '290px' },
]

// null = create mode; otherwise the surface key currently being edited.
const editingKey = ref<string | null>(null)
const formKey = ref('')
const formDisplayName = ref('')
const formType = ref('spa')
const formAccessMode = ref<string>('Authenticated')
const formHost = ref('')
const formPathPrefix = ref('/')
const formBaseUrl = ref('')
const formLocale = ref('')
const formActive = ref(true)

// Template/theme are not edited here (managed via the theme flow / deferred
// template compiler), but the PUT upsert is a full replace — so carry them from
// the edited surface and send them back untouched, never clobbering an assignment.
const carriedTemplatePluginId = ref<string | null>(null)
const carriedTemplateVersion = ref<string | null>(null)
const carriedThemePluginId = ref<string | null>(null)
const carriedThemeVersion = ref<string | null>(null)

// Resolve the workspaces service through the override registry: a plugin may replace it.
const api = useService('workspacesApi', workspacesApi)

// The key (create only), name, type and path prefix are required by the backend.
const canSubmit = computed(
  () =>
    (editingKey.value !== null || formKey.value.trim() !== '') &&
    formDisplayName.value.trim() !== '' &&
    formType.value.trim() !== '' &&
    formPathPrefix.value.trim() !== '',
)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    surfaces.value = await api.listSurfaces(props.workspaceKey)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

function resetForm(): void {
  editingKey.value = null
  formKey.value = ''
  formDisplayName.value = ''
  formType.value = 'spa'
  formAccessMode.value = 'Authenticated'
  formHost.value = ''
  formPathPrefix.value = '/'
  formBaseUrl.value = ''
  formLocale.value = ''
  formActive.value = true
  carriedTemplatePluginId.value = null
  carriedTemplateVersion.value = null
  carriedThemePluginId.value = null
  carriedThemeVersion.value = null
}

function startEdit(surface: WorkspaceSurface): void {
  editingKey.value = surface.surfaceKey
  formKey.value = surface.surfaceKey
  formDisplayName.value = surface.displayName
  formType.value = surface.surfaceType
  formAccessMode.value = surface.accessMode
  formHost.value = surface.publicHost ?? ''
  formPathPrefix.value = surface.publicPathPrefix
  formBaseUrl.value = surface.publicBaseUrl ?? ''
  formLocale.value = surface.locale ?? ''
  formActive.value = surface.isActive
  carriedTemplatePluginId.value = surface.templatePluginId
  carriedTemplateVersion.value = surface.templateVersion
  carriedThemePluginId.value = surface.themePluginId
  carriedThemeVersion.value = surface.themeVersion
}

// A before-save hook may enrich the mutable fields or veto; the key is the
// read-only identity of the surface being saved.
interface SurfaceSaveDraft {
  readonly surfaceKey: string
  readonly isEdit: boolean
  displayName: string
  surfaceType: string
  accessMode: string
  publicHost: string | null
  publicPathPrefix: string
  publicBaseUrl: string | null
  locale: string | null
  isActive: boolean
}

async function save(): Promise<void> {
  if (!canSubmit.value) {
    return
  }
  const key = editingKey.value ?? formKey.value.trim()
  error.value = null
  const draft: SurfaceSaveDraft = {
    surfaceKey: key,
    isEdit: editingKey.value !== null,
    displayName: formDisplayName.value.trim(),
    surfaceType: formType.value.trim(),
    accessMode: formAccessMode.value,
    publicHost: formHost.value.trim() || null,
    publicPathPrefix: formPathPrefix.value.trim(),
    publicBaseUrl: formBaseUrl.value.trim() || null,
    locale: formLocale.value.trim() || null,
    isActive: formActive.value,
  }
  const before = await runHook('workspaces.surface.before-save', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }
  saving.value = true
  try {
    await api.upsertSurface(props.workspaceKey, key, {
      displayName: draft.displayName,
      surfaceType: draft.surfaceType,
      publicBaseUrl: draft.publicBaseUrl,
      publicHost: draft.publicHost,
      publicPathPrefix: draft.publicPathPrefix,
      accessMode: draft.accessMode,
      locale: draft.locale,
      templatePluginId: carriedTemplatePluginId.value,
      templateVersion: carriedTemplateVersion.value,
      themePluginId: carriedThemePluginId.value,
      themeVersion: carriedThemeVersion.value,
      isActive: draft.isActive,
    })
    await runHook('workspaces.surface.after-save', { workspaceKey: props.workspaceKey, surfaceKey: key })
    toast.success(draft.isEdit ? `Surface „${key}“ gespeichert.` : `Surface „${key}“ angelegt.`)
    resetForm()
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

async function remove(surface: WorkspaceSurface): Promise<void> {
  if (busyKey.value === surface.surfaceKey) {
    return
  }
  const confirmed = await confirm({
    title: `Surface „${surface.surfaceKey}“ löschen?`,
    description: `Der Zugang über ${surface.publicHost || ''}${surface.publicPathPrefix} steht danach nicht mehr zur Verfügung.`,
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  error.value = null
  const before = await runHook('workspaces.surface.before-remove', {
    workspaceKey: props.workspaceKey,
    surfaceKey: surface.surfaceKey,
  })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  busyKey.value = surface.surfaceKey
  try {
    await api.removeSurface(props.workspaceKey, surface.surfaceKey)
    await runHook('workspaces.surface.after-remove', {
      workspaceKey: props.workspaceKey,
      surfaceKey: surface.surfaceKey,
    })
    toast.success(`Surface „${surface.surfaceKey}“ gelöscht.`)
    // If the edited surface was the one removed, drop the stale edit state.
    if (editingKey.value === surface.surfaceKey) {
      resetForm()
    }
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyKey.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.surfaces__location {
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
}

.surfaces__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}

.surfaces__editor {
  margin-top: var(--cal-space-4);
}

.surfaces__form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--cal-space-4);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
