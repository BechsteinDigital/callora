/**
 * The read-only context the SSR SurfaceShell hands the client runtime through the
 * #callora-app root's data-* attributes. Deliberately minimal — the grundgerüst only
 * needs to know which workspace/surface it renders; richer context (locale, tokens)
 * is added when a consuming plugin needs it.
 */
export interface SurfaceContext {
  workspaceKey: string
  surfaceKey: string
  caller: SurfaceCaller
}

/** Who a request belongs to. Stable identity is issuer + subjectId, never the subject alone. */
export interface SurfaceSubject {
  issuer: string
  subjectId: string
}

/**
 * Who is using the surface (ADR-017 §3). A caller always exists — the common case sits
 * between "anonymous" and "logged in": the recognised guest with a cart or a draft.
 *
 * The two states are a discriminated union on purpose. If both simply carried a
 * subject, code would eventually check that a subject exists instead of checking
 * authentication, and hang an entitlement off a guest context anyone can obtain.
 */
export type SurfaceCaller =
  | { state: 'guest'; subject: SurfaceSubject }
  | {
      state: 'authenticated'
      subject: SurfaceSubject
      displayName: string
      claims: Record<string, string[]>
    }

const GUEST_ISSUER = 'callora.surface-guest'

/** Reads the surface context off a single element's data-* attributes. */
export function readSurfaceContext(root: HTMLElement): SurfaceContext {
  return {
    workspaceKey: root.dataset.workspace ?? 'default',
    surfaceKey: root.dataset.surface ?? 'default',
    caller: readCaller(root),
  }
}

/**
 * Resolves the surface context for an element that may not carry the data-* itself —
 * an island inside SSR content inherits it from the nearest ancestor that does (the
 * content template puts data-workspace on a wrapper). The #callora-app root carries
 * it directly, so this also covers the whole-app case.
 */
export function resolveSurfaceContext(el: HTMLElement): SurfaceContext {
  const source = el.closest<HTMLElement>('[data-workspace]') ?? el
  return readSurfaceContext(source)
}

function readCaller(root: HTMLElement): SurfaceCaller {
  const subject: SurfaceSubject = {
    issuer: root.dataset.callerIssuer || GUEST_ISSUER,
    subjectId: root.dataset.callerSubject ?? '',
  }

  // Anything other than an explicit "authenticated" is a guest. Defaulting the other
  // way would turn a missing or malformed attribute into an identity.
  if (root.dataset.callerState !== 'authenticated') {
    return { state: 'guest', subject: { issuer: GUEST_ISSUER, subjectId: subject.subjectId } }
  }

  return {
    state: 'authenticated',
    subject,
    displayName: root.dataset.callerName ?? '',
    claims: readClaims(root.dataset.callerClaims),
  }
}

function readClaims(raw: string | undefined): Record<string, string[]> {
  if (!raw) {
    return {}
  }

  try {
    const parsed: unknown = JSON.parse(raw)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return {}
    }

    const claims: Record<string, string[]> = {}
    for (const [key, value] of Object.entries(parsed as Record<string, unknown>)) {
      if (Array.isArray(value)) {
        claims[key] = value.map(String)
      }
    }
    return claims
  } catch {
    // A malformed claim bag must not take the page down: the caller is still known,
    // it just carries nothing a plugin can branch on.
    return {}
  }
}
