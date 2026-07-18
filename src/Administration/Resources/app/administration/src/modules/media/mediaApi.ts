import { apiFetch, unwrap } from '@/core/http'

// Mirrors MediaItemSnapshot (Core). Bytes are addressed by id; the content is
// fetched through contentUrl(), never a client-supplied path.
export interface MediaItem {
  id: string
  workspaceKey: string
  fileName: string
  contentType: string
  sizeBytes: number
  folder: string
  createdBy: string | null
  createdAtUtc: string
}

// Mirrors MediaUploadPolicy: audio for announcements, images for branding (SVG
// deliberately excluded), and a hard size cap. Used for client-side pre-checks.
export const MEDIA_MAX_SIZE_BYTES = 25 * 1024 * 1024
export const MEDIA_ALLOWED_CONTENT_TYPES = [
  'audio/wav',
  'audio/x-wav',
  'audio/mpeg',
  'audio/ogg',
  'image/png',
  'image/jpeg',
  'image/webp',
]

const basePath = '/api/media'

export const mediaApi = {
  // Every media route is workspace-scoped: the workspace key travels as a query
  // parameter and the backend enforces access to it.
  async list(workspaceKey: string, folder?: string): Promise<MediaItem[]> {
    const params = new URLSearchParams({ workspaceKey })
    if (folder) {
      params.set('folder', folder)
    }
    const res = await unwrap(await apiFetch(`${basePath}?${params.toString()}`))
    const page = (await res.json()) as { items: MediaItem[] }
    return page.items
  },

  async upload(workspaceKey: string, file: File, folder?: string): Promise<MediaItem> {
    const params = new URLSearchParams({ workspaceKey })
    if (folder) {
      params.set('folder', folder)
    }
    // Multipart body: the browser sets the multipart boundary content-type itself.
    const form = new FormData()
    form.append('file', file)
    return (await unwrap(await apiFetch(`${basePath}?${params.toString()}`, { method: 'POST', body: form }))).json()
  },

  async remove(workspaceKey: string, id: string): Promise<void> {
    const params = new URLSearchParams({ workspaceKey })
    await unwrap(
      await apiFetch(`${basePath}/${encodeURIComponent(id)}?${params.toString()}`, { method: 'DELETE' }),
    )
  },

  // Same-origin URL for inline preview/download; cookie auth applies to the
  // <img>/<audio> request.
  contentUrl(workspaceKey: string, id: string): string {
    const params = new URLSearchParams({ workspaceKey })
    return `${basePath}/${encodeURIComponent(id)}/content?${params.toString()}`
  },
}
