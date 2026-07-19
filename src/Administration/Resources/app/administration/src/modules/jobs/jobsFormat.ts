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

// Classifies a job status into a badge tone. The backend enum is Pending/Running/
// Succeeded/Failed (BackgroundJobStatus); the substring set stays a bit broader so
// related wordings map too — failed/error → danger, succeeded/complete/done →
// success, everything else (pending, running) → neutral. Note "succe" (not
// "success") is what actually matches "Succeeded".
export function statusTone(status: string): 'danger' | 'success' | 'neutral' {
  const s = status.trim().toLowerCase()
  if (s.includes('fail') || s.includes('dead') || s.includes('error')) {
    return 'danger'
  }
  if (s.includes('succe') || s.includes('complet') || s.includes('done')) {
    return 'success'
  }
  return 'neutral'
}
