export type UnauthorizedHandler = () => void

let onUnauthorized: UnauthorizedHandler = () => {}

export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler
}

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const res = await fetch(path, { credentials: 'include', ...init })
  if (res.status === 401) {
    onUnauthorized()
  }
  return res
}

// Returns the response when ok; otherwise surfaces the RFC 9457 problem detail
// (detail → title → status) as an Error message for the caller to display.
export async function unwrap(res: Response): Promise<Response> {
  if (res.ok) {
    return res
  }
  const problem = (await res.json().catch(() => null)) as { detail?: string; title?: string } | null
  throw new Error(problem?.detail ?? problem?.title ?? `HTTP ${res.status}`)
}

export function jsonInit(method: string, body: unknown): RequestInit {
  return { method, headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }
}
