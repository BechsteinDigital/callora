<script setup lang="ts">
import { computed } from 'vue'
import type { Binding, BlockDefinition } from '@callora/surface'
import type { LayoutBlock } from './layout-document'
import {
  contextKeyOf,
  displayValue,
  fieldsFor,
  valuesOf,
  type ControlField,
} from './control-fields'
import type { TokenRole } from './token-roles'

/**
 * Das Konfigurationspanel eines Blocks — generiert aus seinen `controls`.
 *
 * Nichts hier weiß, was ein bestimmter Block kann. Der Block sagt es über seinen Vertrag, das
 * Panel folgt. Ein von Hand gebautes Panel je Block wäre eine zweite Beschreibung derselben
 * Einstellungen, und die zweite ist immer die, die veraltet.
 */
const props = defineProps<{
  /** Der Block aus dem Dokument — seine Werte. */
  block: LayoutBlock
  /** Seine Definition aus der Registry, oder undefined bei einem verwaisten Block. */
  definition: BlockDefinition | undefined
  /** Die Token-Rollen, die im Canvas tatsächlich gelten. */
  tokenRoles: readonly TokenRole[]
}>()

const emit = defineEmits<{
  change: [control: string, binding: Binding<unknown>]
  clear: [control: string]
  remove: []
}>()

const fields = computed<ControlField[]>(() =>
  fieldsFor(
    props.definition?.controls,
    valuesOf(props.block.config, props.definition?.controls),
    props.tokenRoles,
  ),
)

function bindingOf(name: string): Binding<unknown> | undefined {
  return props.block.config?.[name]
}

function valueOf(field: ControlField): unknown {
  return displayValue(bindingOf(field.name), field.control)
}

/** Ob der Wert dieses Controls im Dokument steht — nur dann gibt es etwas zurückzusetzen. */
function isBound(name: string): boolean {
  return bindingOf(name) !== undefined
}

function setStatic(field: ControlField, value: unknown): void {
  emit('change', field.name, { source: 'static', value })
}

function setNumber(field: ControlField, raw: string): void {
  // Ein leeres Feld ist keine 0. Es zurückzusetzen lässt den Default des Blocks wieder gelten,
  // statt eine Null einzufrieren, die niemand eingegeben hat.
  if (raw === '') {
    emit('clear', field.name)
    return
  }

  const parsed = Number(raw)
  if (!Number.isNaN(parsed)) {
    setStatic(field, parsed)
  }
}

function setContextKey(field: ControlField, key: string): void {
  if (key === '') {
    emit('clear', field.name)
    return
  }

  emit('change', field.name, { source: 'context', key })
}
</script>

<template>
  <aside class="composer-inspector">
    <header class="composer-inspector__header">
      <h2>{{ definition?.label ?? block.blockId }}</h2>
      <p v-if="!definition" class="composer-inspector__orphan" role="status">
        Das Plugin dieses Blocks ist nicht geladen. Seine Einstellungen bleiben im Layout
        erhalten, lassen sich aber erst wieder ändern, wenn es zurück ist.
      </p>
    </header>

    <p v-if="definition && fields.length === 0" class="composer-inspector__empty">
      Dieser Block hat keine Einstellungen.
    </p>

    <div v-for="field in fields" :key="field.name" class="composer-inspector__field">
      <label :for="`control-${field.name}`">
        {{ field.control.label }}
        <!--
          Vertraulich heißt: Der Wert wird serverseitig aufgelöst und erscheint nie im
          ausgelieferten Markup (§7.5). Wer ihn hier eingibt, soll das wissen.
        -->
        <span v-if="field.control.confidential" class="composer-inspector__confidential">
          wird nicht ausgeliefert
        </span>
      </label>
      <p v-if="field.control.description" class="composer-inspector__description">
        {{ field.control.description }}
      </p>

      <input
        v-if="field.kind === 'text'"
        :id="`control-${field.name}`"
        type="text"
        :value="valueOf(field) ?? ''"
        @input="setStatic(field, ($event.target as HTMLInputElement).value)"
      />

      <input
        v-else-if="field.kind === 'number'"
        :id="`control-${field.name}`"
        type="number"
        :min="field.control.min"
        :max="field.control.max"
        :value="valueOf(field) ?? ''"
        @input="setNumber(field, ($event.target as HTMLInputElement).value)"
      />

      <input
        v-else-if="field.kind === 'toggle'"
        :id="`control-${field.name}`"
        type="checkbox"
        :checked="valueOf(field) === true"
        @change="setStatic(field, ($event.target as HTMLInputElement).checked)"
      />

      <select
        v-else-if="field.kind === 'choice'"
        :id="`control-${field.name}`"
        :value="valueOf(field) ?? ''"
        @change="setStatic(field, ($event.target as HTMLSelectElement).value)"
      >
        <option value="">—</option>
        <option v-for="option in field.options" :key="option.value" :value="option.value">
          {{ option.label }}
        </option>
      </select>
      <!--
        Eine Erscheinungs-Auswahl ohne Rollen heißt nicht „keine Einstellung", sondern „das
        Theme setzt hier nichts". Ohne diesen Satz sieht ein leeres Auswahlfeld aus wie ein
        Fehler im Editor.
      -->
      <p
        v-if="field.kind === 'choice' && field.options.length === 0"
        class="composer-inspector__hint"
      >
        Das Theme bringt für diesen Zweck keine Token mit.
      </p>

      <input
        v-else-if="field.kind === 'contextKey'"
        :id="`control-${field.name}`"
        type="text"
        placeholder="plugin.schlüssel/v1"
        :value="contextKeyOf(bindingOf(field.name))"
        @input="setContextKey(field, ($event.target as HTMLInputElement).value)"
      />

      <!--
        Ein Control-Typ, für den es hier noch kein Feld gibt, wird BENANNT statt weggelassen.
        Weggelassen sähe er aus wie eine Einstellung, die es nicht gibt — und niemand käme auf
        die Idee, sie zu vermissen.
      -->
      <p v-else-if="field.kind === 'unsupported'" class="composer-inspector__unsupported">
        Für den Typ <code>{{ field.control.type }}</code> gibt es hier noch kein Eingabefeld.
      </p>

      <button
        v-if="isBound(field.name)"
        type="button"
        class="composer-inspector__reset"
        @click="emit('clear', field.name)"
      >
        Zurücksetzen
      </button>
    </div>

    <!--
      Auch ein verwaister Block lässt sich entfernen. Ihn nur löschen zu können, solange sein
      Plugin da ist, hieße: Wer ein Plugin deinstalliert hat, wird seine Reste nicht mehr los.
    -->
    <button type="button" class="composer-inspector__remove" @click="emit('remove')">
      Block entfernen
    </button>
  </aside>
</template>
