// Pure presentation helpers for the job monitor, kept separate for unit tests.

// Renders an ISO timestamp in the local (de) format; a null/blank value → em dash.
export function formatTimestamp(iso: string | null): string {
  if (!iso) {
    return '—'
  }
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return iso
  }
  return date.toLocaleString('de-DE')
}

// Classifies a job status into a badge tone without hard-coding the exact enum
// names — failed/dead → danger, completed → success, everything else → neutral.
export function statusTone(status: string): 'danger' | 'success' | 'neutral' {
  const s = status.trim().toLowerCase()
  if (s.includes('fail') || s.includes('dead') || s.includes('error')) {
    return 'danger'
  }
  if (s.includes('complet') || s.includes('success') || s.includes('done')) {
    return 'success'
  }
  return 'neutral'
}
