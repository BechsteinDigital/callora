import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * Das Farbschema-Skript muss vor dem ersten Paint laufen und darf dabei zwei Fallen nicht
 * wieder auslösen, die beide erst im Browser sichtbar wurden.
 *
 * Inline ging nicht: Die Content-Security-Policy des Hosts sendet `script-src 'self'` ohne
 * 'unsafe-inline', der Browser führte das Skript also nie aus — und die Seite sprang bei
 * jedem Laden vom System- auf das gewählte Schema um, genau das, was es verhindern soll.
 *
 * Relativ ging auch nicht: Die SPA liefert dieselbe index.html unter JEDEM Pfad unterhalb
 * von /admin/ aus. Auf /admin/extensions/communication löste `./theme-preference.js` zu
 * /admin/extensions/theme-preference.js auf — 404, und derselbe Sprung.
 */
describe('theme preference bootstrap', () => {
  // process.cwd() wie in den anderen Datei-lesenden Tests: import.meta.url ist in der
  // happy-dom-Umgebung keine file:-URL.
  const html = readFileSync(`${process.cwd()}/index.html`, 'utf8')

  it('runs from its own file, because the CSP forbids inline script', () => {
    const inline = /<script(?![^>]*\bsrc=)[^>]*>[\s\S]*?<\/script>/.exec(html)
    expect(inline, `Inline-Skript gefunden: ${inline?.[0]?.slice(0, 120)}`).toBeNull()
  })

  it('is referenced through the base, so deep routes resolve it too', () => {
    expect(html).toContain('src="%BASE_URL%theme-preference.js"')
  })

  it('is not deferred, because it has to run before the first paint', () => {
    const tag = /<script[^>]*theme-preference\.js[^>]*>/.exec(html)?.[0] ?? ''
    expect(tag).not.toMatch(/\b(defer|async)\b/)
  })
})
