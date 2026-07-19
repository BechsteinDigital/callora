/**
 * The read-only context the SSR SurfaceShell hands the client runtime through the
 * #callora-app root's data-* attributes. Deliberately minimal — the grundgerüst only
 * needs to know which workspace/surface it renders; richer context (locale, tokens)
 * is added when a consuming plugin needs it.
 */
export interface SurfaceContext {
  workspaceKey: string
  surfaceKey: string
}

/** Reads the surface context off a single element's data-* attributes. */
export function readSurfaceContext(root: HTMLElement): SurfaceContext {
  return {
    workspaceKey: root.dataset.workspace ?? 'default',
    surfaceKey: root.dataset.surface ?? 'default',
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
