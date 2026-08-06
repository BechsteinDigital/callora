/**
 * Extracts the extension points a source file declares.
 *
 * Pure by design: the generator (`bin/generate-catalog.mjs`) walks the files, this reads them —
 * so the interesting part is testable without a filesystem.
 *
 * Deliberately regex-based rather than AST-based. The two call shapes are fixed by convention
 * (`<ExtensionSlot name="…">` and `runHook('…')`), a parser would pull in a heavy dependency for
 * no gain, and a missed point is caught by the catalog test rather than shipping silently.
 */

export type ExtensionPointKind = 'slot' | 'hook'

export interface ExtensionPoint {
  readonly kind: ExtensionPointKind
  /** Dotted name, or a `*`-suffixed pattern when the call interpolates. */
  readonly name: string
  /** Path of the declaring file, relative to the scan root. */
  readonly file: string
  /** True when the name is assembled at runtime and only its prefix is known. */
  readonly dynamic?: boolean
}

// `name="…"` anywhere in the opening tag, so attribute order does not matter.
const SLOT = /<ExtensionSlot[^>]*\sname="([^"]+)"/g
// A call, not a declaration: `runHook(` followed directly by a string literal.
const HOOK_LITERAL = /runHook\(\s*['"]([^'"]+)['"]/g
// `runHook(\`prefix-${…}\`)` — only the prefix is knowable before runtime.
const HOOK_TEMPLATE = /runHook\(\s*`([^`$]*)\$\{/g

/**
 * Removes comments before scanning.
 *
 * Without this, any file that *writes about* extension points is read as if it declared them —
 * this module's own documentation quotes `runHook('…')` as an example and was duly catalogued
 * under the name `…`. A doc comment must never become a contract.
 *
 * Crude on purpose: it also mangles `//` inside string literals and URLs. That costs nothing,
 * because the result is only ever searched for the two call shapes below, never executed or
 * re-emitted.
 */
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/\/\/.*$/gm, ' ')
}

export function scanExtensionPoints(rawSource: string, file: string): ExtensionPoint[] {
  const source = stripComments(rawSource)
  const found: ExtensionPoint[] = []
  const seen = new Set<string>()

  const add = (point: ExtensionPoint): void => {
    const key = `${point.kind}:${point.name}`
    if (seen.has(key)) {
      return
    }
    seen.add(key)
    found.push(point)
  }

  for (const [, name] of source.matchAll(SLOT)) {
    add({ kind: 'slot', name, file })
  }

  for (const [, name] of source.matchAll(HOOK_LITERAL)) {
    add({ kind: 'hook', name, file })
  }

  // Recording the prefix as a pattern beats dropping it: a plugin author sees that the family
  // exists, even though no single literal name can be offered for it.
  for (const [, prefix] of source.matchAll(HOOK_TEMPLATE)) {
    add({ kind: 'hook', name: `${prefix}*`, file, dynamic: true })
  }

  return found
}
