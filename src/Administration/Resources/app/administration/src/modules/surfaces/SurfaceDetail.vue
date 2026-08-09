<template>
  <div class="detail">
    <header class="detail__header">
      <div class="detail__identity">
        <p class="detail__breadcrumb">{{ breadcrumb }}</p>
        <h1 class="detail__title">{{ formDisplayName || formKey || 'Neue Fläche' }}</h1>
        <p v-if="!isNew" class="detail__address">{{ address }}</p>
      </div>
      <div class="detail__actions">
        <CalButton v-if="isNew" variant="ghost" @click="emit('cancel')">Abbrechen</CalButton>
        <CalButton
          v-else-if="canManage"
          variant="danger-ghost"
          :icon="Trash2"
          icon-only
          title="Löschen"
          @click="remove"
        />
        <CalButton v-if="canManage" :loading="saving" :disabled="!canSubmit" @click="save">Speichern</CalButton>
      </div>
    </header>

    <CalAlert v-if="error" tone="danger" class="detail__error">{{ error }}</CalAlert>

    <CalTabs v-model="activeTab" :tabs="tabs">
      <template #general>
        <CalCard>
          <form class="detail__form" @submit.prevent="save">
            <CalField v-slot="{ id }" label="Schlüssel" required>
              <CalInput :id="id" v-model="formKey" name="surfaceKey" :disabled="!isNew" />
            </CalField>
            <CalField v-slot="{ id }" label="Name" required>
              <CalInput :id="id" v-model="formDisplayName" name="displayName" />
            </CalField>
            <CalField
              v-slot="{ id }"
              label="URL-Segment"
              required
              :description="formParentKey
                ? 'Nur das eigene Segment — der volle Pfad entsteht aus der Kette.'
                : 'Der Einstiegspfad dieser Fläche.'"
            >
              <CalInput :id="id" v-model="formPathPrefix" name="publicPathPrefix" />
            </CalField>
            <CalField v-slot="{ id }" label="Übergeordnet" description="Leer heißt: eine eigene Wurzel.">
              <CalSelect :id="id" v-model="formParentKey" name="parentSurfaceKey">
                <option value="">— eigene Wurzel —</option>
                <option v-for="candidate in parentCandidates" :key="candidate.surfaceKey" :value="candidate.surfaceKey">
                  {{ candidate.displayName }} ({{ candidate.surfaceKey }})
                </option>
              </CalSelect>
            </CalField>
            <CalField
              v-slot="{ id }"
              label="App"
              hint="optional"
              description="Wer diese Fläche bedient. Ohne App ist sie eine Inhaltsfläche und wird über den Layout-Reiter gestaltet."
            >
              <CalSelect :id="id" v-model="formApp" name="templatePluginId">
                <option value="">— Inhaltsfläche —</option>
                <option v-for="plugin in appCandidates" :key="plugin.pluginId" :value="plugin.pluginId">
                  {{ plugin.displayName }}
                </option>
              </CalSelect>
            </CalField>
            <CalField v-slot="{ id }" label="Adressierung">
              <!--
                Abgeleitet, nicht gewählt: Eine Fläche, die einer App gehört, deutet ihre
                Unterpfade selbst — ein Konferenzraum entsteht zur Laufzeit und kann kein Knoten
                sein. Zwei Felder für eine Entscheidung hätten sich widersprechen können, und der
                Widerspruch wäre erst als 404 auf einer echten Adresse aufgefallen (ADR-022).
              -->
              <CalInput :id="id" :model-value="routingLabels[formRouting] ?? formRouting" name="routing" disabled />
            </CalField>
            <CalField v-slot="{ id }" label="Eigener Host" hint="optional">
              <CalInput :id="id" v-model="formHost" name="publicHost" />
            </CalField>
            <CalField v-slot="{ id }" label="Sprache" hint="optional">
              <CalInput :id="id" v-model="formLocale" name="locale" placeholder="de" />
            </CalField>
            <CalField label="Zustand">
              <CalSwitch v-model="formActive" name="isActive">Aktiv</CalSwitch>
            </CalField>

            <ExtensionSlot name="surfaces.detail.fields" :ctx="slotContext" />
          </form>
        </CalCard>
      </template>

      <template #layout>
        <!-- Der Editor gehört einem Plugin, nicht dem Host. Der Reiter ist ein SLOT: Ohne
             Composer steht hier ein Hinweis statt eines toten Reiters, und ein zweiter
             Editor könnte sich daneben hängen, ohne dass der Host ihn kennt. -->
        <ExtensionSlot name="surfaces.detail.layout" :ctx="slotContext" />
        <CalEmptyState
          v-if="!hasLayoutEditor"
          :icon="LayoutTemplate"
          title="Kein Editor installiert"
          description="Ein Plugin liefert den Layout-Editor für diese Fläche — ohne eines bleibt hier nichts zu tun."
        />
      </template>

      <!--
        Was die App an ihrer eigenen Fläche führt — Räume, Vorgänge, was auch immer sie
        verwaltet. Dynamische Slot-Namen, weil erst zur Laufzeit feststeht, welche Reiter es
        gibt: Sie hängen an der App-Zuweisung, nicht an einer Liste im Host.
      -->
      <template v-for="tab in appTabs" :key="tab.id" #[tab.id]>
        <component :is="tab.component" :ctx="slotContext" />
      </template>

      <template #access>
        <CalCard>
          <div class="detail__form">
            <CalField v-slot="{ id }" label="Zugang">
              <CalSelect :id="id" v-model="formAuthentication" name="authentication">
                <option v-for="mode in authentications" :key="mode" :value="mode">
                  {{ authenticationLabels[mode] ?? mode }}
                </option>
              </CalSelect>
            </CalField>
            <CalField
              v-slot="{ id }"
              label="Erforderliche Claims"
              hint="kommagetrennt"
              description="Kumulativ entlang der Kette: Was ein Elternteil verlangt, gilt auch hier."
            >
              <CalInput :id="id" v-model="formRequiredClaims" name="requiredClaims" />
            </CalField>
            <CalField v-if="inherited.length" label="Von übergeordneten Knoten">
              <div class="detail__claims">
                <CalBadge v-for="claim in inherited" :key="claim" tone="neutral">{{ claim }}</CalBadge>
              </div>
            </CalField>

            <ExtensionSlot name="surfaces.detail.access" :ctx="slotContext" />
          </div>
        </CalCard>
      </template>
    </CalTabs>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { LayoutTemplate, Trash2 } from 'lucide-vue-next'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import CalSwitch from '@/core/ui/CalSwitch.vue'
import CalTabs from '@/core/ui/CalTabs.vue'
import type { TabItem } from '@/core/ui/tabs'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { getExtensions } from '@/core/extensions/registry'
import { surfaceTabsFor } from '@/core/extensions/surfaceTabs'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'
import { useService } from '@/core/extensions/services'
import {
  workspacesApi,
  SURFACE_AUTHENTICATIONS,
  SURFACE_AUTHENTICATION_LABELS,
  SURFACE_ROUTING_LABELS,
  type WorkspaceSurface,
} from '@/modules/workspaces/workspacesApi'
import { eligibleParents, inheritedClaims, parseClaims } from '@/modules/workspaces/surfaceTree'

const props = defineProps<{
  workspaceKey: string
  surface: WorkspaceSurface | null
  surfaces: readonly WorkspaceSurface[]
  parentKey: string | null
  canManage: boolean
}>()

const emit = defineEmits<{
  saved: [surfaceKey: string]
  removed: []
  cancel: []
}>()

const api = useService('workspacesApi', workspacesApi)
const authentications = SURFACE_AUTHENTICATIONS
const authenticationLabels = SURFACE_AUTHENTICATION_LABELS
const routingLabels = SURFACE_ROUTING_LABELS

const isNew = computed(() => props.surface === null)
const activeTab = ref('general')
const saving = ref(false)
const error = ref<string | null>(null)

const formKey = ref('')
const formDisplayName = ref('')
const formPathPrefix = ref('')
const formParentKey = ref('')
const formApp = ref<string>('')
const appCandidates = ref<{ pluginId: string; displayName: string }[]>([])
/**
 * Folgt der App-Zuweisung. Eine Fläche mit App wird von ihr bedient; was unter ihr liegt,
 * bestimmt sie. Ohne App ist der Baum die Wahrheit.
 */
const formRouting = computed(() => (formApp.value ? 'Application' : 'Tree'))
const formAuthentication = ref<string>('Public')
const formHost = ref('')
const formLocale = ref('')
const formActive = ref(true)
const formRequiredClaims = ref('')
const formGrantedClaims = ref('')

// Template und Theme reisen unverändert mit: Das Speichern ist ein vollständiges Ersetzen,
// und was das Formular nicht mitschickt, löscht der Server.
const carriedTemplateVersion = ref<string | null>(null)
const carriedThemePluginId = ref<string | null>(null)
const carriedThemeVersion = ref<string | null>(null)
const carriedPublicBaseUrl = ref<string | null>(null)
const carriedPosition = ref(0)

/**
 * Die Reiter der zugewiesenen App. Aus dem GESPEICHERTEN Wert, nicht aus dem Formularfeld:
 * Solange nicht gespeichert ist, gibt es die Zuweisung serverseitig nicht — ein Reiter, dessen
 * Inhalt gleich eine API ohne passende Fläche befragte, zeigte nur einen Fehler.
 */
const appTabs = computed(() => surfaceTabsFor(props.surface?.templatePluginId ?? null))

const tabs = computed<TabItem[]>(() => [
  { value: 'general', label: 'Allgemein' },
  { value: 'layout', label: 'Layout' },
  ...appTabs.value.map((tab) => ({ value: tab.id, label: tab.label })),
  { value: 'access', label: 'Zugriff' },
])

const hasLayoutEditor = computed(() => getExtensions('surfaces.detail.layout').length > 0)

const parentCandidates = computed(() =>
  eligibleParents(props.surfaces, props.surface?.surfaceKey ?? null),
)

const inherited = computed(() =>
  props.surface ? inheritedClaims(props.surfaces, props.surface.surfaceKey) : [],
)

const breadcrumb = computed(() => {
  const byKey = new Map(props.surfaces.map((surface) => [surface.surfaceKey, surface]))
  const names: string[] = []
  const seen = new Set<string>()
  let key: string | null = formParentKey.value || null
  while (key && !seen.has(key)) {
    seen.add(key)
    const node = byKey.get(key)
    if (!node) {
      break
    }
    names.unshift(node.displayName || node.surfaceKey)
    key = node.parentSurfaceKey
  }
  return [props.workspaceKey, ...names].join(' › ')
})

/** Was im Baum steht, nicht was der Server ausrechnet — die Kette kennt nur er (ADR-021). */
const address = computed(() => {
  const segment = formPathPrefix.value.replace(/^\/+/, '')
  const base = formHost.value || props.workspaceKey
  return formHost.value ? `//${base}/${segment}` : `/${base}${segment ? `/${segment}` : ''}`
})

// Was ein Plugin über den ausgewählten Knoten wissen muss, um seinen Editor zu zeigen.
const slotContext = computed(() => ({
  workspaceKey: props.workspaceKey,
  surfaceKey: props.surface?.surfaceKey ?? null,
  routing: props.surface?.routing ?? formRouting.value,
}))

const canSubmit = computed(
  () =>
    formKey.value.trim() !== '' &&
    formDisplayName.value.trim() !== '' &&
    formPathPrefix.value.trim() !== '',
)

function fill(): void {
  const surface = props.surface
  if (!surface) {
    formKey.value = ''
    formDisplayName.value = ''
    formPathPrefix.value = ''
    formParentKey.value = props.parentKey ?? ''
    formApp.value = ''
    formAuthentication.value = 'Public'
    formHost.value = ''
    formLocale.value = ''
    formActive.value = true
    formRequiredClaims.value = ''
    formGrantedClaims.value = ''
    carriedTemplateVersion.value = null
    carriedThemePluginId.value = null
    carriedThemeVersion.value = null
    carriedPublicBaseUrl.value = null
    carriedPosition.value = 0
    return
  }

  formKey.value = surface.surfaceKey
  formDisplayName.value = surface.displayName
  formPathPrefix.value = surface.publicPathPrefix
  formParentKey.value = surface.parentSurfaceKey ?? ''
  formApp.value = surface.templatePluginId ?? ''
  formAuthentication.value = surface.authentication
  formHost.value = surface.publicHost ?? ''
  formLocale.value = surface.locale ?? ''
  formActive.value = surface.isActive
  formRequiredClaims.value = parseClaims(surface.requiredClaims).join(', ')
  formGrantedClaims.value = parseClaims(surface.grantedClaims).join(', ')
  carriedTemplateVersion.value = surface.templateVersion
  carriedThemePluginId.value = surface.themePluginId
  carriedThemeVersion.value = surface.themeVersion
  carriedPublicBaseUrl.value = surface.publicBaseUrl
  carriedPosition.value = surface.position ?? 0
}

async function save(): Promise<void> {
  if (!canSubmit.value || saving.value) {
    return
  }

  const key = props.surface?.surfaceKey ?? formKey.value.trim()
  saving.value = true
  error.value = null
  try {
    await api.upsertSurface(props.workspaceKey, key, {
      displayName: formDisplayName.value.trim(),
      surfaceType: props.surface?.surfaceType ?? 'spa',
      publicBaseUrl: carriedPublicBaseUrl.value,
      publicHost: formHost.value.trim() || null,
      publicPathPrefix: formPathPrefix.value.trim(),
      authentication: formAuthentication.value,
      routing: formRouting.value,
      locale: formLocale.value.trim() || null,
      templatePluginId: formApp.value || null,
      templateVersion: carriedTemplateVersion.value,
      themePluginId: carriedThemePluginId.value,
      themeVersion: carriedThemeVersion.value,
      isActive: formActive.value,
      parentSurfaceKey: formParentKey.value || null,
      position: carriedPosition.value,
      requiredClaims: parseClaims(formRequiredClaims.value).join(',') || null,
      grantedClaims: parseClaims(formGrantedClaims.value).join(',') || null,
    })
    toast.success(`Fläche „${key}" gespeichert.`)
    emit('saved', key)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

async function remove(): Promise<void> {
  const surface = props.surface
  if (!surface) {
    return
  }

  const ok = await confirm({
    title: `Fläche „${surface.displayName || surface.surfaceKey}" löschen?`,
    description: 'Ihre Unterseiten verlieren damit ihren Elternknoten und erscheinen als eigene Wurzeln.',
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!ok) {
    return
  }

  try {
    await api.removeSurface(props.workspaceKey, surface.surfaceKey)
    toast.success(`Fläche „${surface.surfaceKey}" gelöscht.`)
    emit('removed')
  } catch (e) {
    error.value = (e as Error).message
  }
}

/**
 * Die Apps zur Auswahl: die diesem Workspace zugewiesenen und dort aktiven Plugins.
 *
 * Ein Plugin ohne eigenes Flächen-Entry bleibt wirkungslos — der Renderpfad fällt dann auf die
 * eingebaute Shell zurück. Das ist der harmlose Fehlerfall; die Alternative wäre ein weiteres
 * Manifest-Feld, das jeder Plugin-Autor zu setzen hätte, bevor seine App überhaupt wählbar wäre.
 */
async function loadAppCandidates(): Promise<void> {
  try {
    const assignments = await api.listPlugins(props.workspaceKey)
    appCandidates.value = assignments
      .filter((assignment) => assignment.isActive && assignment.isAssigned)
      .map((assignment) => ({ pluginId: assignment.pluginId, displayName: assignment.displayName }))
  } catch {
    // Kein roter Kasten für eine Auswahl: Wer nichts zuweisen kann, sieht eine leere Liste und
    // eine Fläche, die weiterhin speicherbar ist.
    appCandidates.value = []
  }
}

onMounted(loadAppCandidates)

watch(() => [props.surface, props.parentKey], fill, { immediate: true })
</script>

<style scoped lang="scss">
.detail__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--cal-space-4);
  margin-bottom: var(--cal-space-4);
}

.detail__identity {
  min-width: 0;
}

.detail__breadcrumb {
  margin: 0;
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.detail__title {
  margin: 0;
  font-size: var(--cal-text-xl);
  font-weight: var(--cal-weight-semibold);
  letter-spacing: -0.01em;
}

.detail__address {
  margin: var(--cal-space-1) 0 0;
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.detail__actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  flex: none;
}

.detail__error {
  margin-bottom: var(--cal-space-4);
}

.detail__form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: var(--cal-space-4);
}

.detail__claims {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cal-space-2);
}
</style>
