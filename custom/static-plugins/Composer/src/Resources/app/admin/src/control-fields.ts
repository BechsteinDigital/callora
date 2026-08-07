import type { Binding, BlockControl, BlockControls } from '@callora/surface'
import { isTokenControl, rolesForControlType, type TokenRole } from './token-roles'

/**
 * Aus welchem Feld ein Control besteht — die Entscheidung, aus der das Panel generiert wird.
 *
 * Generiert, nicht handgeschrieben: §7.7. Ein handgepflegtes Panel je Block wäre eine zweite
 * Beschreibung derselben Einstellungen neben `BlockControls`, und zwei Beschreibungen laufen
 * auseinander. Der Block sagt, was er kann; das Panel folgt.
 */

/** Welche Art Eingabe ein Control bekommt. */
export type FieldKind = 'text' | 'number' | 'toggle' | 'choice' | 'contextKey' | 'unsupported'

export interface ControlField {
  /** Der Name im `config`-Objekt des Blocks. */
  name: string
  control: BlockControl
  kind: FieldKind
  /** Nur bei `choice`: die Werte, aus denen gewählt werden darf. */
  options: readonly { value: string; label: string }[]
}

const KIND_BY_TYPE: Readonly<Record<string, FieldKind>> = {
  text: 'text',
  number: 'number',
  toggle: 'toggle',
  select: 'choice',
  variant: 'choice',
  context: 'contextKey',
}

/**
 * Die Felder für einen Block, in Deklarationsreihenfolge, gefiltert nach `visibleWhen`.
 *
 * `visibleWhen` ist Framers `hidden` umgedreht: Zu sagen, WANN etwas gilt, liest sich wie der
 * Autor denkt — und ein fehlendes Prädikat heißt dann „immer", nicht „nie".
 */
export function fieldsFor(
  controls: BlockControls | undefined,
  values: Readonly<Record<string, unknown>>,
  tokenRoles: readonly TokenRole[],
): ControlField[] {
  return Object.entries(controls ?? {})
    .filter(([, control]) => control.visibleWhen?.(values) ?? true)
    .map(([name, control]) => ({
      name,
      control,
      kind: kindOf(control),
      options: optionsFor(control, tokenRoles),
    }))
}

function kindOf(control: BlockControl): FieldKind {
  if (isTokenControl(control.type)) {
    return 'choice'
  }

  // Ein Typ ohne Feld wird BENANNT, nicht weggelassen. Weggelassen sähe er aus wie ein
  // Control, das es nicht gibt; benannt ist er eine sichtbare Lücke, die jemand füllen kann.
  return KIND_BY_TYPE[control.type] ?? 'unsupported'
}

function optionsFor(
  control: BlockControl,
  tokenRoles: readonly TokenRole[],
): readonly { value: string; label: string }[] {
  if (isTokenControl(control.type)) {
    // Erscheinung wählt aus --cal-* und aus nichts sonst. Die eigenen `options` eines
    // Controls werden hier bewusst NICHT berücksichtigt: Sie wären der Weg, auf dem ein
    // freier Farbwert doch noch hereinkäme.
    return rolesForControlType(control.type, tokenRoles).map((role) => ({
      value: role,
      label: role,
    }))
  }

  return control.options ?? []
}

/**
 * Der Wert, den ein Feld anzeigt: der gebundene, sonst der Default des Controls.
 *
 * Nur `static` hat hier einen Wert. Eine `context`-Bindung hat einen Schlüssel und erst zur
 * Laufzeit einen Wert; einen zu erfinden hieße, dem Redakteur etwas zu zeigen, das die Fläche
 * so nicht rendert.
 */
export function displayValue(binding: Binding<unknown> | undefined, control: BlockControl): unknown {
  if (binding?.source === 'static') {
    return binding.value
  }

  return control.default
}

/** Der Kontext-Schlüssel einer Bindung, oder leer. */
export function contextKeyOf(binding: Binding<unknown> | undefined): string {
  return binding?.source === 'context' ? binding.key : ''
}

/**
 * Die Werte eines Blocks als flaches Objekt — was `visibleWhen` bekommt.
 *
 * Ein Prädikat fragt „ist `mode` gleich `advanced`", nicht „ist die Bindung von `mode` eine
 * statische mit dem Wert `advanced`". Es sieht Werte, nicht Bindungen.
 */
export function valuesOf(
  config: Readonly<Record<string, Binding<unknown>>> | undefined,
  controls: BlockControls | undefined,
): Record<string, unknown> {
  const values: Record<string, unknown> = {}
  for (const [name, control] of Object.entries(controls ?? {})) {
    values[name] = displayValue(config?.[name], control)
  }

  return values
}
