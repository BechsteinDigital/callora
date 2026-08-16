import { readFileSync } from 'node:fs'
import { describe, it, expect, vi, afterEach } from 'vitest'
import { nextTick } from 'vue'
import { mount, type VueWrapper } from '@vue/test-utils'
import AppCommandPalette from './AppCommandPalette.vue'
import type { CommandItem } from './commandItem'

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))

const commands: CommandItem[] = [
  { id: 'nav:/users', label: 'Benutzer', section: 'Navigation', to: '/users' },
  { id: 'nav:/roles', label: 'Rollen', section: 'Navigation', to: '/roles' },
  { id: 'action:logout', label: 'Abmelden', section: 'Aktionen', run: () => undefined },
]

let mounted: VueWrapper | null = null

// Die Palette rendert durch ein Portal in den Body, nicht in den Wrapper — gesucht wird deshalb
// im Dokument, so wie eine Vorlesehilfe es auch täte.
async function openPalette(): Promise<{ input: HTMLInputElement; listbox: HTMLElement }> {
  mounted = mount(AppCommandPalette, { props: { open: true, commands }, attachTo: document.body })
  await nextTick()
  await nextTick()

  return {
    input: document.querySelector<HTMLInputElement>('input[role="combobox"]')!,
    listbox: document.querySelector<HTMLElement>('[role="listbox"]')!,
  }
}

function pressArrowDown(input: HTMLInputElement): Promise<void> {
  input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }))
  return nextTick()
}

afterEach(() => {
  mounted?.unmount()
  mounted = null
  document.body.replaceChildren()
})

describe('Befehlspalette und die Tastatur', () => {
  // Der Fokus bleibt im Eingabefeld, während die Auswahl wandert. Ohne aria-activedescendant
  // erfährt eine Vorlesehilfe von dieser Bewegung nichts — die Pfeiltaste tut dann sichtbar
  // etwas und hörbar nichts.
  it('nennt die aktive Option am Eingabefeld und zieht sie mit', async () => {
    const { input } = await openPalette()

    expect(input.getAttribute('aria-controls')).toBe('palette-results')
    const first = input.getAttribute('aria-activedescendant')
    expect(first).toBeTruthy()

    await pressArrowDown(input)

    const second = input.getAttribute('aria-activedescendant')
    expect(second).not.toBe(first)
    expect(document.getElementById(second!)).not.toBeNull()
  })

  // ARIA erlaubt in einer listbox keine beliebigen Kinder. Standen die Überschriften direkt
  // darin, durfte eine Vorlesehilfe die ganze Liste als fehlerhaft übergehen.
  it('führt jede Sektion als group, statt Überschriften in die listbox zu legen', async () => {
    const { listbox } = await openPalette()

    const groups = Array.from(listbox.querySelectorAll(':scope > [role="group"]'))
    expect(groups).toHaveLength(2)
    expect(groups[0].getAttribute('aria-labelledby')).toBeTruthy()

    for (const child of Array.from(listbox.children)) {
      const role = child.getAttribute('role')
      // Direktes Kind ohne group-Rolle darf nur die Leermeldung sein.
      expect(role === 'group' || child.className.includes('palette__empty')).toBe(true)
    }
  })

  // Die Markierung wanderte aus dem Sichtfeld: Ab dem siebten Eintrag sah man nichts mehr und
  // musste raten, wo man ist.
  it('scrollt die aktive Option in den sichtbaren Bereich', async () => {
    const scrollIntoView = vi.fn()
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      value: scrollIntoView,
      configurable: true,
    })

    const { input } = await openPalette()
    await pressArrowDown(input)

    expect(scrollIntoView).toHaveBeenCalledWith({ block: 'nearest' })
  })
})

describe('die Shell und die Tastatur', () => {
  // Ohne ihn führt jeder Tastaturweg in eine neue Ansicht zuerst durch die komplette
  // Seitenleiste. Die öffentliche Fläche hat den Link seit jeher — geprüft wird hier deshalb
  // dieselbe Zusage für die Administration.
  it('bietet einen Sprung zum Inhalt an, und er zeigt auf das main-Element', () => {
    const shell = readFileSync(`${process.cwd()}/src/app/AppShell.vue`, 'utf8')

    expect(shell).toContain('href="#shell-content"')
    expect(shell).toMatch(/<main[^>]*id="shell-content"[^>]*tabindex="-1"/)
    // Vor der Seitenleiste, sonst überspringt er nichts.
    expect(shell.indexOf('shell__skip')).toBeLessThan(shell.indexOf('<AppSidebar'))
  })
})
