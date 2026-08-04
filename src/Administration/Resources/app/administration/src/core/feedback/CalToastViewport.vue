<template>
  <Teleport to="body">
    <div class="cal-toasts" role="region" aria-label="Meldungen">
      <TransitionGroup name="cal-toast">
        <div
          v-for="item in toasts"
          :key="item.id"
          class="cal-toast"
          :class="`is-${item.tone}`"
          :role="item.tone === 'danger' ? 'alert' : 'status'"
        >
          <CalIcon class="cal-toast__icon" :icon="iconFor(item.tone)" size="sm" />
          <div class="cal-toast__body">
            <p class="cal-toast__message">{{ item.message }}</p>
            <p v-if="item.description" class="cal-toast__description">{{ item.description }}</p>
          </div>
          <button type="button" class="cal-toast__close" aria-label="Meldung schließen" @click="dismiss(item.id)">
            <CalIcon :icon="X" size="sm" />
          </button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-vue-next'
import CalIcon from '@/core/ui/CalIcon.vue'
import { useToasts } from './toasts'
import type { ToastTone } from './toast'

// Mounted once in the app shell; teleported to <body> so it floats above every
// route, dialog and dropdown regardless of where the reporting code lives.
const { toasts, dismiss } = useToasts()

const ICONS = {
  success: CheckCircle2,
  info: Info,
  warning: AlertTriangle,
  danger: XCircle,
}

function iconFor(tone: ToastTone) {
  return ICONS[tone]
}
</script>

<style scoped lang="scss">
.cal-toasts {
  position: fixed;
  right: var(--cal-space-5);
  bottom: var(--cal-space-5);
  z-index: var(--cal-z-toast);
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
  width: min(380px, calc(100vw - var(--cal-space-8)));
  pointer-events: none;
}

.cal-toast {
  display: flex;
  align-items: flex-start;
  gap: var(--cal-space-3);
  padding: var(--cal-space-3) var(--cal-space-4);
  background: var(--cal-surface-raised);
  border: 1px solid var(--cal-border);
  border-left: 3px solid var(--cal-border-strong);
  border-radius: var(--cal-radius-md);
  box-shadow: var(--cal-shadow-lg);
  pointer-events: auto;
}

.cal-toast.is-success {
  border-left-color: var(--cal-success);
}

.cal-toast.is-info {
  border-left-color: var(--cal-info);
}

.cal-toast.is-warning {
  border-left-color: var(--cal-warning);
}

.cal-toast.is-danger {
  border-left-color: var(--cal-danger);
}

.cal-toast.is-success .cal-toast__icon {
  color: var(--cal-success);
}

.cal-toast.is-info .cal-toast__icon {
  color: var(--cal-info);
}

.cal-toast.is-warning .cal-toast__icon {
  color: var(--cal-warning);
}

.cal-toast.is-danger .cal-toast__icon {
  color: var(--cal-danger);
}

.cal-toast__icon {
  margin-top: 2px;
}

.cal-toast__body {
  flex: 1;
  min-width: 0;
}

.cal-toast__message {
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
  overflow-wrap: anywhere;
}

.cal-toast__description {
  margin-top: 2px;
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
  overflow-wrap: anywhere;
}

.cal-toast__close {
  flex: none;
  padding: 2px;
  border: 0;
  border-radius: var(--cal-radius-xs);
  background: none;
  color: var(--cal-text-muted);
  cursor: pointer;
}

.cal-toast__close:hover {
  color: var(--cal-text);
  background: var(--cal-surface-hover);
}

.cal-toast-enter-active,
.cal-toast-leave-active {
  transition:
    opacity var(--cal-duration-base) var(--cal-ease-out),
    transform var(--cal-duration-base) var(--cal-ease-out);
}

.cal-toast-enter-from {
  opacity: 0;
  transform: translateY(8px) scale(0.97);
}

.cal-toast-leave-to {
  opacity: 0;
  transform: translateX(12px);
}
</style>
