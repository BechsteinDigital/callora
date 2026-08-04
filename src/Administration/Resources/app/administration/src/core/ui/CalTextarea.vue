<template>
  <textarea
    v-bind="$attrs"
    class="cal-textarea"
    :class="{ 'is-invalid': invalid, 'is-mono': mono }"
    :value="modelValue"
    :rows="rows"
    :disabled="disabled"
    :placeholder="placeholder"
    :aria-invalid="invalid || undefined"
    @input="$emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
  />
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    modelValue: string
    rows?: number
    placeholder?: string
    // JSON payloads (flow definitions, webhook bodies) read far better in a
    // monospaced face — the alternative was a hand-styled textarea per module.
    mono?: boolean
    invalid?: boolean
    disabled?: boolean
  }>(),
  { rows: 4, mono: false, invalid: false, disabled: false },
)

defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<style scoped lang="scss">
.cal-textarea {
  display: block;
  width: 100%;
  padding: var(--cal-space-2) var(--cal-space-3);
  background: var(--cal-surface-inset);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-sm);
  color: var(--cal-text);
  font-size: var(--cal-text-md);
  line-height: var(--cal-leading-normal);
  resize: vertical;
  transition:
    border-color var(--cal-duration-fast) var(--cal-ease),
    background var(--cal-duration-fast) var(--cal-ease);
}

.cal-textarea.is-mono {
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
}

.cal-textarea:hover:not(:disabled) {
  border-color: var(--cal-border-strong);
}

.cal-textarea:focus {
  outline: none;
  border-color: var(--cal-accent);
  background: var(--cal-surface);
  box-shadow: 0 0 0 3px var(--cal-accent-subtle);
}

.cal-textarea.is-invalid {
  border-color: var(--cal-danger);
}

.cal-textarea::placeholder {
  color: var(--cal-text-muted);
}

.cal-textarea:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
