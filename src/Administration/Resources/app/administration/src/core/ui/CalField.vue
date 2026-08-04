<template>
  <div class="cal-field" :class="{ 'is-horizontal': horizontal }">
    <div class="cal-field__head">
      <label class="cal-field__label" :for="inputId">
        {{ label }}
        <span v-if="required" class="cal-field__required" aria-hidden="true">*</span>
      </label>
      <span v-if="hint && !error" class="cal-field__hint">{{ hint }}</span>
    </div>
    <div class="cal-field__control">
      <slot :id="inputId" />
      <p v-if="error" class="cal-field__error">{{ error }}</p>
      <p v-else-if="description" class="cal-field__description">{{ description }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, useId } from 'vue'

/**
 * Label, help text and error message around one control. The generated id is
 * exposed through the slot so the control can bind it and stay associated with
 * the label — every form in the shell gets that association for free.
 */
const props = defineProps<{
  label: string
  /** Short note next to the label, e.g. a unit or format. */
  hint?: string
  /** Longer explanation under the control. Hidden while an error is shown. */
  description?: string
  error?: string
  required?: boolean
  /** Label beside the control instead of above — for dense settings lists. */
  horizontal?: boolean
  id?: string
}>()

const generatedId = useId()
const inputId = computed(() => props.id ?? generatedId)
</script>

<style scoped lang="scss">
.cal-field {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
}

.cal-field.is-horizontal {
  display: grid;
  grid-template-columns: minmax(140px, 240px) 1fr;
  align-items: start;
  gap: var(--cal-space-4);
}

.cal-field__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--cal-space-3);
}

.cal-field__label {
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
  color: var(--cal-text);
}

.cal-field__required {
  color: var(--cal-danger);
}

.cal-field__hint {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.cal-field__control {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-1);
  min-width: 0;
}

.cal-field__description {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
  line-height: var(--cal-leading-normal);
}

.cal-field__error {
  font-size: var(--cal-text-sm);
  color: var(--cal-danger);
}

@media (width <= 720px) {
  .cal-field.is-horizontal {
    grid-template-columns: 1fr;
    gap: var(--cal-space-2);
  }
}
</style>
