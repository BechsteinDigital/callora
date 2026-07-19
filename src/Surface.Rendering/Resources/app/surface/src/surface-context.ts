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

/** Reads the surface context off the SSR-rendered mount root. */
export function readSurfaceContext(root: HTMLElement): SurfaceContext {
  return {
    workspaceKey: root.dataset.workspace ?? 'default',
    surfaceKey: root.dataset.surface ?? 'default',
  }
}
