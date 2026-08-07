<template>
  <div class="surfaces">
    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="rows"
        row-key="surfaceKey"
        :loading="loading"
        :error="error"
        :empty-icon="Layers"
        empty-title="Keine Surfaces."
        empty-description="Der öffentliche Zugang läuft über die default-Surface."
      >
        <!--
          Der Schlüssel trägt die Einrückung: Ein Baum, der nur in der Reihenfolge steckt, ist
          keiner — man sähe eine Liste, deren Sortierung niemand erklären kann.
        -->
        <template #cell-surfaceKey="{ row }">
          <span class="surfaces__key" :style="{ '--depth': row.depth }">
            <span v-if="row.depth > 0" class="surfaces__branch" aria-hidden="true">└</span>
            {{ row.surfaceKey }}
          </span>
        </template>

        <template #cell-location="{ row }">
          <!--
            Der volle Pfad, nicht das gespeicherte Segment: Ein Kind trägt `partner`, erreichbar
            ist es unter `/portal/partner`. Das Segment anzuzeigen hieße, eine URL zu behaupten,
            die es nicht gibt.
          -->
          <span class="surfaces__location">{{ row.effectiveHost || '—' }}{{ row.effectivePath }}</span>
        </template>

        <template #cell-theme="{ row }">
          <!--
            „geerbt" statt „vom Workspace": Seit ADR-019 kommt das Design eines Kindes von
            seinem nächsten Vorfahren, der eines setzt — nicht vom Workspace.
          -->
          <CalBadge :tone="row.themePluginId ? 'accent' : 'neutral'" variant="outline">
            {{ row.themePluginId ? row.themePluginId : row.depth > 0 ? 'geerbt' : 'vom Workspace' }}
          </CalBadge>
        </template>

        <!--
          Eigene und geerbte Anforderungen getrennt: Was von oben gilt, kann man hier nicht
          ändern, und es sähe sonst aus wie eine Einstellung dieses Knotens.
        -->
        <template #cell-visibility="{ row }">
          <span v-if="row.ownClaims.length === 0 && row.inheritedClaims.length === 0">Alle</span>
          <template v-else>
            <CalBadge v-for="claim in row.ownClaims" :key="claim" tone="warning" variant="outline">
              {{ claim }}
            </CalBadge>
            <CalBadge
              v-for="claim in row.inheritedClaims"
              :key="`geerbt-${claim}`"
              tone="neutral"
              variant="outline"
              :title="'Von einer übergeordneten Fläche gefordert'"
            >
              {{ claim }} ↑
            </CalBadge>
          </template>
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
        <CalField
          v-slot="{ id }"
          label="Übergeordnet"
          hint="optional"
          description="Ohne Übergeordnetes ist die Surface eine eigene Anwendung und trägt Host, Zugang und Design selbst. Darunter erbt sie beides."
        >
          <CalSelect :id="id" v-model="formParentKey" name="surfaceParent">
            <option value="">— eigene Anwendung —</option>
            <option v-for="candidate in parentCandidates" :key="candidate.surfaceKey" :value="candidate.surfaceKey">
              {{ candidate.displayName }} ({{ candidate.surfaceKey }})
            </option>
          </CalSelect>
        </CalField>
        <CalField v-slot="{ id }" label="Zugang">
          <CalSelect :id="id" v-model="formAccessMode" name="surfaceAccessMode">
            <option v-for="mode in accessModes" :key="mode" :value="mode">{{ mode }}</option>
          </CalSelect>
        </CalField>
        <CalField v-slot="{ id }" label="Öffentlicher Host" hint="optional">
          <CalInput :id="id" v-model="formHost" name="surfaceHost" />
        </CalField>
        <CalField
          v-slot="{ id }"
          label="Pfad-Präfix"
          required
          :description="formParentKey
            ? 'Nur das eigene Segment — der volle Pfad entsteht aus der Kette.'
            : undefined"
        >
          <CalInput :id="id" v-model="formPathPrefix" name="surfacePathPrefix" />
        </CalField>
        <CalField
          v-slot="{ id }"
          label="Sichtbar für"
          hint="optional"
          :description="formInheritedClaims.length > 0
            ? `Zusätzlich gefordert von oben: ${formInheritedClaims.join(', ')}`
            : 'Claims, die ein Besucher mitbringen muss — kommagetrennt. Leer heißt: für alle sichtbar.'"
        >
          <CalInput :id="id" v-model="formRequiredClaims" name="surfaceRequiredClaims" />
        </CalField>
        <CalField v-slot="{ id }" label="Reihenfolge" hint="unter Geschwistern">
          <CalInput :id="id" v-model="formPosition" type="number" name="surfacePosition" />
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
import { eligibleParents, flattenSurfaceTree, inheritedClaims, parseClaims } from './surfaceTree'
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

/**
 * Die Surfaces in Baum-Reihenfolge, jede mit ihrer Tiefe und ihrem VOLLEN Pfad.
 *
 * Der volle Pfad wird hier zusammengesetzt und nicht vom Server geholt: Die Verwaltung zeigt
 * die eigenen Werte eines Knotens (sonst könnte sie geerbt nicht von gesetzt unterscheiden),
 * und der volle Pfad ist eine Anzeige, keine Eingabe.
 */
const rows = computed(() =>
  flattenSurfaceTree(surfaces.value).map(({ surface, depth }) => {
    const chain = ancestryOf(surface)
    return {
      ...surface,
      depth,
      effectivePath: composePath(chain.map((node) => node.publicPathPrefix)),
      effectiveHost: chain.find((node) => node.publicHost)?.publicHost ?? null,
      ownClaims: parseClaims(surface.requiredClaims),
      inheritedClaims: inheritedClaims(surfaces.value, surface.surfaceKey),
    }
  }),
)

/**
 * Was von oben zusätzlich gefordert wird. Getrennt vom Eingabefeld, weil man es hier nicht
 * ändern darf: Stünde die ganze Kette darin, schriebe ein Speichern die Anforderung des
 * Elternteils hier fest — und ein späteres Lockern dort bliebe wirkungslos.
 */
const formInheritedClaims = computed(() =>
  editingKey.value
    ? inheritedClaims(surfaces.value, editingKey.value)
    : formParentKey.value
      ? [...inheritedClaims(surfaces.value, formParentKey.value),
         ...parseClaims(surfaces.value.find((s) => s.surfaceKey === formParentKey.value)?.requiredClaims)]
      : [],
)

/** Die Kette eines Knotens aufwärts, Knoten zuerst. Bricht ab, statt an einem Zyklus zu hängen. */
function ancestryOf(surface: WorkspaceSurface): WorkspaceSurface[] {
  const byKey = new Map(surfaces.value.map((entry) => [entry.surfaceKey, entry]))
  const chain: WorkspaceSurface[] = [surface]
  const seen = new Set([surface.surfaceKey])

  let parentKey = surface.parentSurfaceKey
  while (parentKey && !seen.has(parentKey)) {
    const parent = byKey.get(parentKey)
    if (!parent) {
      break
    }

    chain.push(parent)
    seen.add(parent.surfaceKey)
    parentKey = parent.parentSurfaceKey
  }

  return chain
}

/** Setzt den vollen Pfad aus der Kette zusammen — dieselbe Regel wie serverseitig. */
function composePath(segmentsFromNodeToRoot: string[]): string {
  const parts = [...segmentsFromNodeToRoot]
    .reverse()
    .map((segment) => segment.trim().replace(/^\/+|\/+$/g, ''))
    .filter((segment) => segment.length > 0)

  return parts.length === 0 ? '/' : `/${parts.join('/')}`
}

/**
 * Was als Übergeordnetes in Frage kommt: nicht der Knoten selbst und keiner seiner Nachfahren.
 * Der Server lehnt einen Zyklus ohnehin ab; ihn gar nicht anzubieten ist der Unterschied
 * zwischen einer Fehlermeldung und einer Auswahl, die nur Mögliches enthält.
 */
const parentCandidates = computed(() => eligibleParents(surfaces.value, editingKey.value))

const columns: readonly DataTableColumn[] = [
  { key: 'surfaceKey', label: 'Schlüssel', mono: true },
  { key: 'displayName', label: 'Name' },
  { key: 'surfaceType', label: 'Typ', width: '100px' },
  { key: 'accessMode', label: 'Zugang', width: '140px' },
  { key: 'location', label: 'Host / Pfad' },
  { key: 'theme', label: 'Design', width: '170px' },
  { key: 'visibility', label: 'Sichtbar für', width: '160px' },
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
const formParentKey = ref('')
const formPosition = ref('0')
const formRequiredClaims = ref('')

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
  formParentKey.value = ''
  formPosition.value = '0'
  formRequiredClaims.value = ''
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
  formParentKey.value = surface.parentSurfaceKey ?? ''
  formPosition.value = String(surface.position)
  formRequiredClaims.value = surface.requiredClaims ?? ''
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
  parentSurfaceKey: string | null
  position: number
  requiredClaims: string | null
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
    parentSurfaceKey: formParentKey.value || null,
    // Ein leeres oder unlesbares Feld heißt 0 — nicht NaN, das der Server als 400 zurückgäbe.
    position: Number.parseInt(formPosition.value, 10) || 0,
    requiredClaims: parseClaims(formRequiredClaims.value).join(',') || null,
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
      parentSurfaceKey: draft.parentSurfaceKey,
      position: draft.position,
      requiredClaims: draft.requiredClaims,
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

/*
 * Die Einrückung trägt die Baumstruktur. Ohne sie sähe man eine Liste, deren Sortierung
 * niemand erklären kann — die Reihenfolge allein ist keine sichtbare Hierarchie.
 */
.surfaces__key {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding-inline-start: calc(var(--depth, 0) * 1.25rem);
}

.surfaces__branch {
  opacity: 0.4;
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
