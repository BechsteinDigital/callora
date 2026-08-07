import { beforeEach, describe, expect, it } from 'vitest'
import {
  applyScopedSurfaceStyles,
  CANVAS_SCOPE,
  scopeSurfaceStyles,
  scopeThemeTokens,
} from './scoped-surface-styles'

/**
 * Was den Canvas zur echten Vorschau macht: dasselbe Stylesheet, das live gilt — aber eingesperrt.
 * Eine Vorschau, die aus einer eigenen Annäherung an die Gestaltung gebaut ist, driftet vom
 * Ergebnis ab, und die Drift fällt erst auf, wenn jemand veröffentlicht hat.
 */
describe('scopeSurfaceStyles', () => {
  it('bounds every rule to the canvas', () => {
    const scoped = scopeSurfaceStyles('.cal-header { color: red; }')

    expect(scoped).toContain(`@scope (.${CANVAS_SCOPE})`)
    expect(scoped).toContain('.cal-header { color: red; }')
  })

  it('rewrites :root so a theme cannot repaint the admin around it', () => {
    // :root ist das Dokumentelement und liegt AUSSERHALB des Scopes — ohne Umschreiben
    // entkäme ein Token-Block dem @scope und färbte die Shell.
    const scoped = scopeSurfaceStyles(':root { --cal-color-bg: #001; }')

    expect(scoped).not.toMatch(/:root\s*\{/)
    expect(scoped).toContain(':scope')
  })

  it.each(['html', 'body'])('rewrites %s for the same reason', (selector) => {
    const scoped = scopeSurfaceStyles(`${selector} { margin: 0; }`)

    expect(scoped).not.toMatch(new RegExp(`(^|[},])\\s*${selector}\\b`, 'm'))
  })

  it('rewrites :root after another rule, not only at the start', () => {
    const scoped = scopeSurfaceStyles('.a { color: red; }\n:root { --cal-x: 1; }')

    expect(scoped).not.toMatch(/}\s*:root/)
  })

  it('leaves a class that merely contains the word body alone', () => {
    const scoped = scopeSurfaceStyles('.page-body { padding: 0; }')

    expect(scoped).toContain('.page-body')
  })
})

describe('scopeThemeTokens', () => {
  it('emits the same custom properties the server would render', () => {
    const css = scopeThemeTokens({ 'color.brand': '#1f6fe5', 'space.md': '1rem' })

    expect(css).toContain('--cal-color-brand: #1f6fe5;')
    expect(css).toContain('--cal-space-md: 1rem;')
  })

  it('puts them on the canvas rather than on :root', () => {
    const css = scopeThemeTokens({ 'color.brand': '#000' })

    expect(css.startsWith(`.${CANVAS_SCOPE} {`)).toBe(true)
    expect(css).not.toContain(':root')
  })
})

describe('applyScopedSurfaceStyles', () => {
  beforeEach(() => {
    document.head.innerHTML = ''
  })

  it('adds the stylesheet once', () => {
    applyScopedSurfaceStyles('.a { color: red; }')

    expect(document.querySelectorAll('style#callora-canvas-styles')).toHaveLength(1)
  })

  it('replaces rather than layers when the theme changes', () => {
    // Zwei Stylesheets ersetzten einander nicht, sie stapelten sich — und das Ergebnis hinge an
    // der Einfügereihenfolge statt am Theme.
    applyScopedSurfaceStyles('.a { color: red; }')
    applyScopedSurfaceStyles('.a { color: blue; }')

    const styles = document.querySelectorAll('style#callora-canvas-styles')
    expect(styles).toHaveLength(1)
    expect(styles[0]?.textContent).toContain('blue')
    expect(styles[0]?.textContent).not.toContain('red')
  })
})
