/**
 * Brings a surface's stylesheet into the canvas — scoped, so it styles the preview and nothing
 * else in the admin shell.
 *
 * This is what makes "der Canvas IST die Vorschau" true rather than a claim. A preview built from
 * its own approximation of the styling drifts from the result, and the drift is invisible until
 * somebody publishes. The way to avoid that is not discipline, it is using the same stylesheet.
 *
 * Two things have to be contained:
 *
 *  - The surface's rules would otherwise style the admin around the canvas. `.cal-header` means
 *    something on both sides.
 *  - The theme's tokens sit on `:root` by design — that is how a surface themes itself. Inside the
 *    admin, `:root` is the admin's, and letting a workspace theme repaint the shell would be
 *    absurd.
 *
 * `@scope` does both in one construct and is what the CSS working group built for exactly this.
 * The alternative — rewriting every selector — has to parse CSS correctly to be safe, and a
 * rewriter that gets one selector wrong is a preview that lies.
 */

/** The class the canvas root carries. Every scoped rule is bounded by it. */
export const CANVAS_SCOPE = 'cal-canvas'

/**
 * Wraps a stylesheet so it applies only inside the canvas.
 *
 * `:root` and `html`/`body` selectors are rewritten to the scope root: a token block that stayed
 * on `:root` would escape `@scope`, because `:root` is the document element and matches from
 * outside the scope regardless.
 */
export function scopeSurfaceStyles(css: string, scope: string = CANVAS_SCOPE): string {
  const rooted = css.replace(/(^|[},])\s*(:root|html|body)\b/g, (_match, before: string) =>
    `${before} :scope`,
  )

  return `@scope (.${scope}) {\n${rooted}\n}`
}

/**
 * Puts the scoped stylesheet into the document, replacing whatever was there for this id.
 *
 * A canvas that swaps themes must not accumulate stylesheets — the second one would not replace
 * the first, it would layer on top, and the result would depend on insertion order rather than on
 * what the theme says.
 */
export function applyScopedSurfaceStyles(
  css: string,
  id = 'callora-canvas-styles',
  doc: Document = document,
): HTMLStyleElement {
  const existing = doc.getElementById(id)
  const style = existing instanceof HTMLStyleElement ? existing : doc.createElement('style')

  style.id = id
  style.textContent = scopeSurfaceStyles(css)

  if (!existing) {
    doc.head.append(style)
  }

  return style
}

/**
 * The theme's tokens as a scoped block. They arrive as the same dotted keys the server renders
 * into `:root` on a real surface, so the canvas paints from exactly what the surface would get.
 */
export function scopeThemeTokens(
  tokens: Readonly<Record<string, string>>,
  scope: string = CANVAS_SCOPE,
): string {
  const declarations = Object.entries(tokens)
    .map(([key, value]) => `  --cal-${key.replace(/[^a-zA-Z0-9]+/g, '-')}: ${value};`)
    .join('\n')

  return `.${scope} {\n${declarations}\n}`
}
