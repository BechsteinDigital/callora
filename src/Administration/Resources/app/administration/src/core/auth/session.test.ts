import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { describe, it, expect, beforeEach } from 'vitest'
import { endSession, SESSION_RESET_EXEMPTIONS } from './session'
import { useAuthStore } from './authStore'
import { toast, useToasts } from '@/core/feedback/toasts'
import { registerExtension, getExtensions } from '@/core/extensions/registry'
import { registerHook, runHook } from '@/core/extensions/hooks'
import { registerService, useService } from '@/core/extensions/services'

beforeEach(() => {
  endSession()
})

describe('endSession', () => {
  it('lässt nichts von der vorigen Sitzung stehen', () => {
    useAuthStore().context.value = { userId: 'a', isOperator: true } as never
    localStorage.setItem('callora.activeWorkspace', 'kunde-a')
    toast.success('Gespeichert.')
    registerExtension('plugins.list.toolbar', {} as never)

    endSession()

    expect(useAuthStore().context.value).toBeNull()
    expect(localStorage.getItem('callora.activeWorkspace')).toBeNull()
    expect(useToasts().toasts.value).toHaveLength(0)
    expect(getExtensions('plugins.list.toolbar')).toHaveLength(0)
  })

  it('räumt auch die Registrierungen aus geladenen Plugin-Bundles weg', async () => {
    // Die Skripte selbst bleiben im Dokument — ein geladenes Skript lässt sich nicht entladen.
    // Was geht, ist ihr Beitrag zur Oberfläche, und der gehört der Sitzung.
    registerHook('users.before-save', (ctx) => ctx.cancel('von A'))
    const fromA = { marker: 'A' }
    registerService('usersApi', fromA)

    endSession()

    expect((await runHook('users.before-save', {})).canceled).toBe(false)
    expect(useService('usersApi', { marker: 'fallback' })).toEqual({ marker: 'fallback' })
  })
})

/**
 * Das Gate aus #293: Ein neues Modul-Singleton mit eigenem `reset*` soll nicht still am
 * Sitzungsende vorbeilaufen. Wer eines hinzufügt, trägt es in `endSession` ein — oder in die
 * Ausnahmeliste, und dann steht dort auch, warum.
 */
describe('die Liste dessen, was beim Sitzungsende fällt', () => {
  it('enthält jede reset-Funktion aus dem Kern', () => {
    const source = readFileSync(`${process.cwd()}/src/core/auth/session.ts`, 'utf8')
    // Auf den AUFRUF, nicht auf das Vorkommen: Der Import bleibt stehen, wenn jemand die Zeile
    // im Rumpf entfernt — ein Gate, das ihn mitzählt, hält gar nichts.
    const missing = coreResetFunctions().filter(
      (name) => !source.includes(`${name}()`) && !SESSION_RESET_EXEMPTIONS.includes(name),
    )

    expect(missing).toEqual([])
  })
})

/**
 * Die andere Hälfte: Es gab zwei Wege aus der Sitzung — die Shell und das Benutzermenü — und
 * keiner räumte auf. Ein dritter käme dazu, ohne dass jemand daran denkt.
 */
describe('jeder Weg aus der Sitzung', () => {
  it('räumt sie auch auf', () => {
    const offenders = sourceFiles()
      .filter((file) => !file.path.endsWith('core/auth/authStore.ts'))
      .filter((file) => /\.logout\(\)/.test(file.source) && !file.source.includes('endSession()'))
      .map((file) => file.path.slice(file.path.lastIndexOf('/src/') + 5))

    expect(offenders).toEqual([])
  })
})

function sourceFiles(): { path: string; source: string }[] {
  const files: { path: string; source: string }[] = []
  const walk = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      if (entry.isDirectory()) {
        walk(path)
      } else if (/\.(ts|vue)$/.test(entry.name) && !entry.name.includes('.test.')) {
        files.push({ path, source: readFileSync(path, 'utf8') })
      }
    }
  }
  walk(`${process.cwd()}/src`)
  return files
}

function coreResetFunctions(): string[] {
  const found: string[] = []
  const walk = (directory: string): void => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      if (entry.isDirectory()) {
        walk(path)
      } else if (entry.name.endsWith('.ts') && !entry.name.endsWith('.test.ts')) {
        for (const match of readFileSync(path, 'utf8').matchAll(/export function (reset\w+)/g)) {
          found.push(match[1])
        }
      }
    }
  }
  walk(`${process.cwd()}/src/core`)
  return found
}
