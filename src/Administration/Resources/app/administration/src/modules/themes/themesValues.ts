// Pure helpers translating between the settings form (text) and the JSON values
// the backend stores. Kept separate from the API client so they are unit-testable.

// Coerce a text input into the JSON value the backend expects: valid JSON is used
// as-is (42, true, "text", {...}); anything else is stored as a JSON string.
export function coerceInputToJsonValue(raw: string): unknown {
  const trimmed = raw.trim()
  try {
    return JSON.parse(trimmed)
  } catch {
    return trimmed
  }
}

// Human-readable rendering of a raw JSON text value for editing: a JSON string
// shows its inner text; anything else shows the raw JSON. Absent/empty → ''.
export function displayJsonValue(raw: string | undefined | null): string {
  if (raw === undefined || raw === null || raw === '') {
    return ''
  }
  try {
    const parsed = JSON.parse(raw)
    return typeof parsed === 'string' ? parsed : raw
  } catch {
    return raw
  }
}
