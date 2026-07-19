<template>
  <section class="surfaces">
    <h2>Surfaces</h2>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Schlüssel</th>
          <th>Name</th>
          <th>Typ</th>
          <th>Zugang</th>
          <th>Host / Pfad</th>
          <th>Aktiv</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="s in surfaces" :key="s.surfaceKey">
          <td class="mono">{{ s.surfaceKey }}</td>
          <td>{{ s.displayName }}</td>
          <td>{{ s.surfaceType }}</td>
          <td>{{ s.accessMode }}</td>
          <td class="mono">{{ s.publicHost || '—' }}{{ s.publicPathPrefix }}</td>
          <td>
            <span class="badge" :class="s.isActive ? 'badge-active' : 'badge-inactive'">
              {{ s.isActive ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
          <td class="actions">
            <button
              v-if="canManage"
              type="button"
              class="link"
              :disabled="busyKey === s.surfaceKey"
              @click="startEdit(s)"
            >
              Bearbeiten
            </button>
            <button
              v-if="canManage"
              type="button"
              class="link-danger"
              :disabled="busyKey === s.surfaceKey"
              @click="remove(s)"
            >
              Löschen
            </button>
            <ExtensionSlot name="workspaces.surfaces.row-actions" :ctx="s" />
          </td>
        </tr>
        <tr v-if="!surfaces.length">
          <td colspan="7" class="empty">Keine Surfaces. Der öffentliche Zugang läuft über die default-Surface.</td>
        </tr>
      </tbody>
    </table>

    <form v-if="canManage" class="surface-form" @submit.prevent="save">
      <h3>{{ editingKey ? `Surface „${editingKey}“ bearbeiten` : 'Surface anlegen' }}</h3>
      <div class="fields">
        <label>Schlüssel
          <BaseInput v-model="formKey" name="surfaceKey" :disabled="editingKey !== null" />
        </label>
        <label>Anzeigename
          <BaseInput v-model="formDisplayName" name="surfaceDisplayName" />
        </label>
        <label>Typ
          <BaseInput v-model="formType" name="surfaceType" />
        </label>
        <label>Zugang
          <select v-model="formAccessMode" name="surfaceAccessMode" class="select">
            <option v-for="mode in accessModes" :key="mode" :value="mode">{{ mode }}</option>
          </select>
        </label>
        <label>Öffentlicher Host <span class="hint">(optional)</span>
          <BaseInput v-model="formHost" name="surfaceHost" />
        </label>
        <label>Pfad-Präfix
          <BaseInput v-model="formPathPrefix" name="surfacePathPrefix" />
        </label>
        <label>Basis-URL <span class="hint">(optional)</span>
          <BaseInput v-model="formBaseUrl" type="url" name="surfaceBaseUrl" />
        </label>
        <label>Locale <span class="hint">(optional)</span>
          <BaseInput v-model="formLocale" name="surfaceLocale" />
        </label>
        <label class="check">
          <input type="checkbox" v-model="formActive" name="surfaceActive" />
          Aktiv
        </label>
      </div>
      <div class="buttons">
        <BaseButton type="submit" :disabled="saving || !canSubmit">
          {{ editingKey ? 'Speichern' : 'Anlegen' }}
        </BaseButton>
        <button v-if="editingKey" type="button" class="link" @click="resetForm">Abbrechen</button>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { workspacesApi, SURFACE_ACCESS_MODES, type WorkspaceSurface } from './workspacesApi'
import BaseButton from '@/core/ui/BaseButton.vue'
import BaseInput from '@/core/ui/BaseInput.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const props = defineProps<{ workspaceKey: string; canManage: boolean }>()

const accessModes = SURFACE_ACCESS_MODES

const surfaces = ref<WorkspaceSurface[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyKey = ref<string | null>(null)
const saving = ref(false)

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
  if (!window.confirm(`Surface „${surface.surfaceKey}“ löschen?`)) {
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
.surfaces {
  margin-top: calc(var(--cal-space) * 3);
}

.surfaces h2 {
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

.mono {
  font-family: var(--cal-font-mono, monospace);
}

.badge {
  font-size: 0.75em;
  border-radius: var(--cal-radius);
  padding: 0 calc(var(--cal-space) * 0.75);
}

.badge-active {
  color: var(--cal-color-accent);
  border: 1px solid var(--cal-color-accent);
}

.badge-inactive {
  color: var(--cal-color-muted);
  border: 1px solid var(--cal-color-muted);
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

.surface-form {
  margin-top: calc(var(--cal-space) * 2);
}

.surface-form h3 {
  font-size: 1em;
  margin-bottom: var(--cal-space);
}

.fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--cal-space) calc(var(--cal-space) * 2);
}

.fields label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
}

.fields label.check {
  flex-direction: row;
  align-items: center;
  gap: var(--cal-space);
}

.select {
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.hint {
  font-size: 0.85em;
}

.buttons {
  display: flex;
  align-items: center;
  gap: calc(var(--cal-space) * 2);
  margin-top: calc(var(--cal-space) * 1.5);
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
