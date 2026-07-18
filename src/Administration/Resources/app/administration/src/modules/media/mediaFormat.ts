// Pure presentation helpers for the media library, kept separate for unit tests.

export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`
  }
  const kb = bytes / 1024
  if (kb < 1024) {
    return `${kb.toFixed(1)} KB`
  }
  return `${(kb / 1024).toFixed(1)} MB`
}

export function isImageType(contentType: string): boolean {
  return contentType.trim().toLowerCase().startsWith('image/')
}

export function isAudioType(contentType: string): boolean {
  return contentType.trim().toLowerCase().startsWith('audio/')
}
