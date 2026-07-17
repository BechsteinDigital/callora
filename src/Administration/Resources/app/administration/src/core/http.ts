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
