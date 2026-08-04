<template>
  <div class="cal-alert" :class="`is-${tone}`" :role="tone === 'danger' ? 'alert' : 'status'">
    <CalIcon class="cal-alert__icon" :icon="icon" size="sm" />
    <div class="cal-alert__body">
      <p v-if="title" class="cal-alert__title">{{ title }}</p>
      <div class="cal-alert__text"><slot /></div>
    </div>
    <button v-if="dismissible" type="button" class="cal-alert__close" aria-label="Schließen" @click="$emit('dismiss')">
      <CalIcon :icon="X" size="sm" />
    </button>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { AlertTriangle, CheckCircle2, Info, XCircle, X } from 'lucide-vue-next'
import CalIcon from './CalIcon.vue'

/**
 * In-page message tied to the content it concerns — the replacement for the
 * `<p class="error">{{ error }}</p>` each module used to hand-roll. Transient
 * feedback about an action belongs in a toast instead.
 */
const props = withDefaults(
  defineProps<{
    tone?: 'info' | 'success' | 'warning' | 'danger'
    title?: string
    dismissible?: boolean
  }>(),
  { tone: 'info', dismissible: false },
)

defineEmits<{ dismiss: [] }>()

const ICONS = {
  info: Info,
  success: CheckCircle2,
  warning: AlertTriangle,
  danger: XCircle,
}

const icon = computed(() => ICONS[props.tone])
</script>

<style scoped lang="scss">
.cal-alert {
  display: flex;
  align-items: flex-start;
  gap: var(--cal-space-3);
  padding: var(--cal-space-3) var(--cal-space-4);
  border: 1px solid;
  border-radius: var(--cal-radius-md);
  font-size: var(--cal-text-md);
  line-height: var(--cal-leading-normal);
}

.cal-alert__icon {
  margin-top: 2px;
}

.cal-alert__body {
  flex: 1;
  min-width: 0;
}

.cal-alert__title {
  font-weight: var(--cal-weight-semibold);
  margin-bottom: 2px;
}

.cal-alert__text {
  color: var(--cal-text-secondary);
  overflow-wrap: anywhere;
}

.cal-alert__close {
  flex: none;
  padding: 2px;
  border: 0;
  background: none;
  color: inherit;
  opacity: 0.6;
  cursor: pointer;
  border-radius: var(--cal-radius-xs);
}

.cal-alert__close:hover {
  opacity: 1;
}

.cal-alert.is-info {
  background: var(--cal-info-subtle);
  border-color: var(--cal-info-border);
  color: var(--cal-info);
}

.cal-alert.is-success {
  background: var(--cal-success-subtle);
  border-color: var(--cal-success-border);
  color: var(--cal-success);
}

.cal-alert.is-warning {
  background: var(--cal-warning-subtle);
  border-color: var(--cal-warning-border);
  color: var(--cal-warning);
}

.cal-alert.is-danger {
  background: var(--cal-danger-subtle);
  border-color: var(--cal-danger-border);
  color: var(--cal-danger);
}
</style>
