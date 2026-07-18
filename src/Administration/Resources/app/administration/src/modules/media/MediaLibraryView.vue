<template>
  <section class="media">
    <header class="head">
      <h1>Medien</h1>
      <div class="head-actions">
        <ExtensionSlot name="media.toolbar" />
      </div>
    </header>

    <label v-if="showPicker && workspaces.length" class="ws-select">Workspace
      <select v-model="selectedWorkspace" name="workspace" class="select" @change="onWorkspaceChange">
        <option v-for="w in workspaces" :key="w.workspaceKey" :value="w.workspaceKey">
          {{ w.displayName }} ({{ w.workspaceKey }})
        </option>
      </select>
    </label>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="notice" class="notice">{{ notice }}</p>

    <form v-if="canManage && selectedWorkspace" class="upload" @submit.prevent="upload">
      <input
        ref="fileInput"
        type="file"
        name="file"
        :accept="acceptTypes"
        class="file"
        @change="onFileChange"
      />
      <input v-model="folder" name="folder" class="folder" placeholder="Ordner (optional)" />
      <BaseButton type="submit" :disabled="uploading || !selectedFile">
        {{ uploading ? 'Lädt hoch…' : 'Hochladen' }}
      </BaseButton>
    </form>

    <p v-if="loading">Lädt…</p>
    <p v-else-if="!selectedWorkspace" class="empty">Kein Workspace ausgewählt.</p>

    <ul v-else class="grid">
      <li v-for="item in items" :key="item.id" class="card">
        <div class="preview">
          <img v-if="isImageType(item.contentType)" :src="contentUrl(item)" :alt="item.fileName" class="thumb" />
          <audio v-else-if="isAudioType(item.contentType)" :src="contentUrl(item)" controls class="audio" />
          <span v-else class="file-icon">Datei</span>
        </div>
        <div class="meta">
          <a :href="contentUrl(item)" target="_blank" rel="noopener" class="name">{{ item.fileName }}</a>
          <span class="sub">{{ item.contentType }} · {{ formatBytes(item.sizeBytes) }} · {{ item.folder }}</span>
        </div>
        <div class="card-actions">
          <button
            v-if="canManage"
            type="button"
            class="link-danger"
            :disabled="busyId === item.id"
            @click="remove(item)"
          >
            Löschen
          </button>
          <ExtensionSlot name="media.item-actions" :ctx="item" />
        </div>
      </li>
      <li v-if="!items.length" class="empty">Keine Medien vorhanden.</li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { mediaApi, MEDIA_ALLOWED_CONTENT_TYPES, MEDIA_MAX_SIZE_BYTES, type MediaItem } from './mediaApi'
import { formatBytes, isImageType, isAudioType } from './mediaFormat'
import { workspacesApi, type Workspace } from '@/modules/workspaces/workspacesApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'media.manage'))
// A workspace-scoped admin manages their own workspace (no workspace.read needed);
// an operator without a fixed workspace picks one from the list.
const fixedWorkspace = computed(() => ctx.value?.workspaceKey ?? null)
const showPicker = computed(() => !fixedWorkspace.value)

// Resolve the media service through the override registry: a plugin may replace it.
const api = useService('mediaApi', mediaApi)

const acceptTypes = MEDIA_ALLOWED_CONTENT_TYPES.join(',')

const workspaces = ref<Workspace[]>([])
const selectedWorkspace = ref('')
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
  return api.contentUrl(selectedWorkspace.value, item.id)
}

async function loadMedia(): Promise<void> {
  if (!selectedWorkspace.value) {
    items.value = []
    loading.value = false
    return
  }
  loading.value = true
  error.value = null
  try {
    items.value = await api.list(selectedWorkspace.value)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function onWorkspaceChange(): Promise<void> {
  notice.value = null
  await loadMedia()
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
    await api.upload(selectedWorkspace.value, file, draft.folder || undefined)
    await runHook('media.after-upload', { workspaceKey: selectedWorkspace.value, fileName: file.name })
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
  if (!window.confirm(`Datei „${item.fileName}“ löschen?`)) {
    return
  }
  error.value = null
  notice.value = null
  const before = await runHook('media.before-delete', { workspaceKey: selectedWorkspace.value, id: item.id })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  busyId.value = item.id
  try {
    await api.remove(selectedWorkspace.value, item.id)
    await runHook('media.after-delete', { workspaceKey: selectedWorkspace.value, id: item.id })
    await loadMedia()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

onMounted(async () => {
  try {
    if (fixedWorkspace.value) {
      selectedWorkspace.value = fixedWorkspace.value
    } else {
      workspaces.value = await workspacesApi.list()
      selectedWorkspace.value = workspaces.value[0]?.workspaceKey ?? ''
    }
  } catch (e) {
    error.value = (e as Error).message
    loading.value = false
    return
  }
  await loadMedia()
})
</script>

<style scoped lang="scss">
.media {
  padding: calc(var(--cal-space) * 3);
}

.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: calc(var(--cal-space) * 2);
}

.head-actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
}

.ws-select {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
  margin-bottom: calc(var(--cal-space) * 1.5);
  max-width: 360px;
}

.select {
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.upload {
  display: flex;
  gap: var(--cal-space);
  align-items: center;
  margin-bottom: calc(var(--cal-space) * 2);
  flex-wrap: wrap;
}

.folder {
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.grid {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: calc(var(--cal-space) * 2);
}

.card {
  border: 1px solid var(--cal-color-surface);
  border-radius: var(--cal-radius);
  padding: var(--cal-space);
  display: flex;
  flex-direction: column;
  gap: var(--cal-space);
}

.preview {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
  background: var(--cal-color-surface);
  border-radius: var(--cal-radius);
}

.thumb {
  max-width: 100%;
  max-height: 160px;
  object-fit: contain;
}

.audio {
  width: 100%;
}

.file-icon {
  color: var(--cal-color-muted);
}

.meta {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.name {
  color: var(--cal-color-accent);
  text-decoration: none;
  word-break: break-all;
}

.sub {
  font-size: 0.8em;
  color: var(--cal-color-muted);
}

.card-actions {
  display: flex;
  gap: calc(var(--cal-space) * 1.5);
  align-items: center;
}

.link-danger {
  background: none;
  border: 0;
  color: var(--cal-color-danger);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link-danger:disabled {
  opacity: 0.5;
  cursor: default;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}

.notice {
  color: var(--cal-color-accent);
}
</style>
