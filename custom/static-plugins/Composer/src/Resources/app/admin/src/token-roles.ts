/**
 * Welche Token-Rollen ein Block anbieten darf.
 *
 * Gelesen aus dem CSS, das im Canvas tatsächlich gilt — nicht aus einer gepflegten Liste.
 * Eine zweite Liste neben `tokens.scss` und den Theme-Werten wäre eine zweite Wahrheit
 * darüber, was es gibt, und sie liefe auseinander, sobald jemand ein Token hinzufügt.
 *
 * Das ist der Guardrail aus §7.7 in ausführbarer Form: Die Erscheinungs-Controls
 * (`colorToken`, `spacingToken`, `typeToken`) wählen aus `--cal-*` und aus nichts sonst.
 * Ein freier Farbwähler würde in einer Registrierung aufheben, was die Token-Achse
 * zusichert.
 */

/** Eine Token-Rolle: der Name ohne `--cal-`-Präfix, etwa `color-fg`. */
export type TokenRole = string

/** Welches Präfix zu welchem Control-Typ gehört. */
const ROLE_PREFIX: Readonly<Record<string, string>> = {
  colorToken: 'color-',
  spacingToken: 'space-',
  typeToken: 'font-',
}

/**
 * Sammelt jede `--cal-*`-Deklaration aus einem CSS-Text.
 *
 * Deklarationen, nicht Verwendungen: `--cal-color-fg: #111` zählt, `var(--cal-color-fg)`
 * nicht. Der Unterschied ist der zwischen „diese Rolle gibt es" und „irgendwer liest sie" —
 * ein Block, der ein Token liest, das niemand setzt, darf es nicht zur Auswahl stellen.
 */
export function collectTokenRoles(css: string): TokenRole[] {
  const found = new Set<TokenRole>()
  for (const match of css.matchAll(/--cal-([a-z0-9-]+)\s*:/gi)) {
    found.add(match[1].toLowerCase())
  }

  return [...found].sort()
}

/**
 * Die Rollen, die ein Control dieses Typs anbieten darf.
 *
 * Ein unbekannter Typ bekommt nichts — nicht alles. Wer einen eigenen Erscheinungs-Typ
 * beiträgt, ohne dass hier ein Präfix dafür steht, soll eine leere Auswahl sehen und
 * nachfragen, statt still die Farbrollen als Abstände angeboten zu bekommen.
 */
export function rolesForControlType(type: string, roles: readonly TokenRole[]): TokenRole[] {
  const prefix = ROLE_PREFIX[type]
  return prefix ? roles.filter((role) => role.startsWith(prefix)) : []
}

/** Ob dieser Control-Typ seine Werte aus den Token-Rollen zieht. */
export function isTokenControl(type: string): boolean {
  return type in ROLE_PREFIX
}
