// Pure helpers for the flow editor, kept separate for unit tests.

/// <summary>
// Parses a JSON textarea value. A blank value yields <paramref name="whenEmpty"/>
// (e.g. null for optional conditions, [] for actions). Invalid JSON throws a
// friendly, labelled Error the view surfaces to the operator.
export function parseJsonField(text: string, whenEmpty: unknown, label: string): unknown {
  const trimmed = text.trim()
  if (!trimmed) {
    return whenEmpty
  }
  try {
    return JSON.parse(trimmed)
  } catch {
    throw new Error(`${label} enthält kein gültiges JSON.`)
  }
}

// Pretty-prints a stored JSON string for editing; a blank/null value → empty box.
export function prettyJson(json: string | null): string {
  if (!json) {
    return ''
  }
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    // Show the raw value if it somehow is not valid JSON, rather than hiding it.
    return json
  }
}
