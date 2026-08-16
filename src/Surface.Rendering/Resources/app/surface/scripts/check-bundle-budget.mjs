#!/usr/bin/env node
// Misst die gebaute Ausgabe gegen bundle-budget.json und BRICHT AB, wenn eine Zahl gerissen ist
// (#297).
//
// Nicht `chunkSizeWarningLimit`: Das schreibt eine Zeile ins Build-Log, die niemand liest —
// dieselbe Sorte Vorkehrung wie ein Circuit Breaker, der nie auslöst. Weil `dotnet build
// Callora.Host.sln` die Frontends mitbaut, hängt das Budget damit am normalen Bauen und Testen.
//
// Es scheitert in BEIDE Richtungen. Zu groß ist der offensichtliche Fall. Zu klein ist der, den
// man vergisst: Ein Budget, das weit über der Wirklichkeit liegt, ist keine Grenze mehr, sondern
// eine Zahl, an der man sich nicht mehr stört. Wie überall hier dürfen die Zahlen nur sinken.
//
// Bewusst kein geteiltes Skript mit der Admin-Suite: Beide sind eigene npm-Pakete, und sie über
// einen Pfad in ein Nachbarverzeichnis aneinanderzuhängen wäre der höhere Preis als eine zweite
// Kopie von sechzig Zeilen.
import { readFileSync, statSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const budget = JSON.parse(readFileSync(resolve(packageRoot, 'bundle-budget.json'), 'utf8'))
const slack = (budget.slackPercent ?? 15) / 100

const failures = []
const measured = []

for (const entry of budget.files) {
  const path = resolve(packageRoot, entry.path)
  let size
  try {
    size = statSync(path).size
  } catch {
    failures.push(`${entry.path}: nicht gebaut — erwartet unter ${path}`)
    continue
  }

  const floor = Math.round(entry.maxBytes * (1 - slack))
  measured.push(`${entry.path}: ${size} B von ${entry.maxBytes} B`)

  if (size > entry.maxBytes) {
    failures.push(
      `${entry.path}: ${size} B über dem Budget von ${entry.maxBytes} B ` +
        `(+${size - entry.maxBytes} B). Entweder kleiner werden oder die Zahl in einem eigenen ` +
        `Commit anheben — mit dem Grund dabei.`,
    )
  } else if (size < floor) {
    failures.push(
      `${entry.path}: ${size} B liegt mehr als ${budget.slackPercent}% unter dem Budget von ` +
        `${entry.maxBytes} B. Bitte das Budget auf etwa ${Math.round(size * (1 + slack / 2))} B ` +
        `nachziehen — eine Grenze, die niemand mehr erreicht, ist keine.`,
    )
  }
}

console.log(`Bundle-Budget:\n  ${measured.join('\n  ')}`)

if (failures.length > 0) {
  console.error(`\nBundle-Budget verletzt:\n  ${failures.join('\n  ')}`)
  process.exit(1)
}
