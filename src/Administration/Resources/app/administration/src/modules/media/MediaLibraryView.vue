<template>
  <CalPage>
    <CalPageHeader title="Medien" description="Dateien des aktiven Workspaces.">
      <template #actions>
        <ExtensionSlot name="media.toolbar" />
      </template>
    </CalPageHeader>

    <CalAlert v-if="error" class="media__message" tone="danger">{{ error }}</CalAlert>
    <CalAlert v-if="notice" class="media__message" tone="success" dismissible @dismiss="notice = null">
      {{ notice }}
    </CalAlert>

    <CalCard v-if="canManage && activeWorkspace" class="media__upload" title="Hochladen">
      <form class="media__form" @submit.prevent="upload">
        <CalField v-slot="{ id }" label="Datei" :description="`Erlaubt: ${acceptTypes}`">
          <input :id="id" ref="fileInput" type="file" name="file" :accept="acceptTypes" class="media__file" @change="onFileChange" />
        </CalField>
        <CalField v-slot="{ id }" label="Ordner" hint="optional">
          <CalInput :id="id" v-model="folder" name="folder" :icon="Folder" />
        </CalField>
        <CalButton type="submit" variant="primary" :icon="Upload" :loading="uploading" :disabled="!selectedFile">
          Hochladen
        </CalButton>
      </form>
    </CalCard>

    <CalCard v-if="loading" class="media__state">
      <div class="media__skeletons">
        <CalSkeleton v-for="n in 4" :key="n" height="150px" />
      </div>
    </CalCard>

    <CalCard v-else-if="!activeWorkspace">
      <CalEmptyState
        :icon="Boxes"
        title="Kein Workspace ausgewählt."
        description="Wählen Sie oben rechts einen Workspace, um dessen Medien zu sehen."
      />
    </CalCard>

    <CalCard v-else-if="!items.length">
      <CalEmptyState
        :icon="Image"
        title="Keine Medien vorhanden."
        description="Laden Sie Bilder oder Audiodateien hoch, um sie in Surfaces und Flows zu verwenden."
      />
    </CalCard>

    <ul v-else class="media__grid">
      <li v-for="item in items" :key="item.id" class="media__item">
        <div class="media__preview">
          <img v-if="isImageType(item.contentType)" :src="contentUrl(item)" :alt="item.fileName" class="media__thumb" />
          <audio v-else-if="isAudioType(item.contentType)" :src="contentUrl(item)" controls class="media__audio" />
          <CalIcon v-else class="media__file-icon" :icon="FileText" size="xl" />
        </div>
        <div class="media__meta">
          <a :href="contentUrl(item)" target="_blank" rel="noopener" class="media__name" :title="item.fileName">
            {{ item.fileName }}
          </a>
          <span class="media__sub">{{ formatBytes(item.sizeBytes) }} · {{ item.folder }}</span>
        </div>
        <div class="media__actions">
          <CalButton
            v-if="canManage"
            variant="danger-ghost"
            size="sm"
            :disabled="busyId === item.id"
            @click="remove(item)"
          >
            Löschen
          </CalButton>
          <ExtensionSlot name="media.item-actions" :ctx="item" />
        </div>
      </li>
    </ul>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Boxes, FileText, Folder, Image, Upload } from 'lucide-vue-next'
import { mediaApi, MEDIA_ALLOWED_CONTENT_TYPES, MEDIA_MAX_SIZE_BYTES, type MediaItem } from './mediaApi'
import { formatBytes, isImageType, isAudioType } from './mediaFormat'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalIcon from '@/core/ui/CalIcon.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'media.manage'))

// The workspace comes from the global context (topbar switcher or the bound
// admin's fixed workspace) — no per-view picker.
const { activeWorkspace, ensure: ensureWorkspace } = useWorkspaceContext()

// Resolve the media service through the override registry: a plugin may replace it.
const api = useService('mediaApi', mediaApi)

const acceptTypes = MEDIA_ALLOWED_CONTENT_TYPES.join(',')

const items = ref<MediaItem[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const uploading = ref(false)
const busyId = ref<string | null>(null)
const folder = ref('')
const fileInput = ref<HTMLInputElement | null>(null)
const selectedFile = ref<File | null>(null)

function contentUrl(item: MediaItem): string {
  return api.contentUrl(activeWorkspace.value, item.id)
}

async function loadMedia(): Promise<void> {
  if (!activeWorkspace.value) {
    items.value = []
    loading.value = false
    return
  }
  loading.value = true
  error.value = null
  try {
    items.value = await api.list(activeWorkspace.value)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

function onFileChange(event: Event): void {
  selectedFile.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

// Mirrors MediaUploadPolicy so the operator gets immediate feedback; the backend
// enforces the same rules regardless.
function validateFile(file: File): string | null {
  if (!MEDIA_ALLOWED_CONTENT_TYPES.includes(file.type)) {
    return `Dateityp „${file.type || 'unbekannt'}“ ist nicht erlaubt.`
  }
  if (file.size <= 0 || file.size > MEDIA_MAX_SIZE_BYTES) {
    return `Die Datei muss zwischen 1 Byte und ${MEDIA_MAX_SIZE_BYTES / (1024 * 1024)} MB groß sein.`
  }
  return null
}

// A before-upload hook may adjust the target folder or veto; the file name is the
// read-only identity of what is being uploaded.
interface MediaUploadDraft {
  readonly fileName: string
  folder: string
}

async function upload(): Promise<void> {
  error.value = null
  notice.value = null
  const file = selectedFile.value
  if (!file) {
    return
  }
  const invalid = validateFile(file)
  if (invalid) {
    error.value = invalid
    return
  }
  // Lock before the (possibly async) hook so a double-click cannot start two uploads.
  uploading.value = true
  try {
    const draft: MediaUploadDraft = { fileName: file.name, folder: folder.value.trim() }
    const before = await runHook('media.before-upload', draft)
    if (before.canceled) {
      error.value = before.cancelReason ?? 'Upload abgebrochen.'
      return
    }
    await api.upload(activeWorkspace.value, file, draft.folder || undefined)
    await runHook('media.after-upload', { workspaceKey: activeWorkspace.value, fileName: file.name })
    if (fileInput.value) {
      fileInput.value.value = ''
    }
    selectedFile.value = null
    folder.value = ''
    notice.value = 'Datei hochgeladen.'
    await loadMedia()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    uploading.value = false
  }
}

async function remove(item: MediaItem): Promise<void> {
  if (busyId.value === item.id) {
    return
  }
  const confirmed = await confirm({
    title: `Datei „${item.fileName}“ löschen?`,
    description: 'Verweise aus Surfaces oder Flows auf diese Datei laufen danach ins Leere.',
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  error.value = null
  notice.value = null
  const before = await runHook('media.before-delete', { workspaceKey: activeWorkspace.value, id: item.id })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  busyId.value = item.id
  try {
    await api.remove(activeWorkspace.value, item.id)
    await runHook('media.after-delete', { workspaceKey: activeWorkspace.value, id: item.id })
    toast.success(`„${item.fileName}“ gelöscht.`)
    await loadMedia()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

// Reload when the active workspace resolves or the operator switches it. The
// immediate run covers a fixed admin's initial load; an operator's first real
// load follows ensure() populating the selection below.
watch(
  activeWorkspace,
  () => {
    notice.value = null
    void loadMedia()
  },
  { immediate: true },
)

onMounted(() => {
  void ensureWorkspace().catch((e) => {
    error.value = (e as Error).message
    loading.value = false
  })
})
</script>

<style scoped lang="scss">
.media__message {
  margin-bottom: var(--cal-space-4);
}

.media__upload {
  margin-bottom: var(--cal-space-4);
}

.media__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-4);
  flex-wrap: wrap;
}

.media__form > :deep(.cal-field) {
  flex: 1;
  min-width: 220px;
}

.media__file {
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
}

.media__file::file-selector-button {
  margin-right: var(--cal-space-3);
  padding: var(--cal-space-1) var(--cal-space-3);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-sm);
  background: var(--cal-surface-raised);
  color: var(--cal-text);
  font: inherit;
  cursor: pointer;
}

.media__state {
  margin-bottom: var(--cal-space-4);
}

.media__skeletons {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--cal-space-4);
}

.media__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--cal-space-4);
}

.media__item {
  display: flex;
  flex-direction: column;
  background: var(--cal-surface);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-lg);
  overflow: hidden;
  transition: border-color var(--cal-duration-fast) var(--cal-ease);
}

.media__item:hover {
  border-color: var(--cal-border-strong);
}

.media__preview {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 140px;
  background: var(--cal-surface-inset);
  color: var(--cal-text-muted);
  overflow: hidden;
}

.media__thumb {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.media__audio {
  width: 90%;
}

.media__meta {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: var(--cal-space-3) var(--cal-space-3) var(--cal-space-2);
  min-width: 0;
}

.media__name {
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
  color: var(--cal-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.media__sub {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.media__actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space-1);
  padding: 0 var(--cal-space-2) var(--cal-space-2);
}
</style>
