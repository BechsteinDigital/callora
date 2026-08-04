<template>
  <DialogRoot :open="open" @update:open="$emit('update:open', $event)">
    <DialogPortal>
      <DialogOverlay class="cal-dialog__overlay" />
      <DialogContent class="cal-dialog" :class="`is-${size}`">
        <header class="cal-dialog__head">
          <DialogTitle class="cal-dialog__title">{{ title }}</DialogTitle>
          <DialogDescription v-if="description" class="cal-dialog__description">
            {{ description }}
          </DialogDescription>
        </header>

        <div v-if="$slots.default" class="cal-dialog__body"><slot /></div>

        <footer v-if="$slots.footer" class="cal-dialog__footer"><slot name="footer" /></footer>

        <DialogClose class="cal-dialog__close" aria-label="Schließen">
          <CalIcon :icon="X" size="sm" />
        </DialogClose>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>

<script setup lang="ts">
import {
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'radix-vue'
import { X } from 'lucide-vue-next'
import CalIcon from './CalIcon.vue'

// Radix carries the hard parts: focus trap, restore on close, Escape, scroll
// lock and the aria wiring. We supply the surface and nothing else.
withDefaults(defineProps<{ open: boolean; title: string; description?: string; size?: 'sm' | 'md' | 'lg' }>(), {
  size: 'sm',
})

defineEmits<{ 'update:open': [value: boolean] }>()
</script>

<style scoped lang="scss">
.cal-dialog__overlay {
  position: fixed;
  inset: 0;
  z-index: var(--cal-z-overlay);
  background: var(--cal-overlay-backdrop);
  backdrop-filter: blur(2px);
  animation: cal-dialog-fade var(--cal-duration-base) var(--cal-ease-out);
}

.cal-dialog {
  position: fixed;
  top: 50%;
  left: 50%;
  z-index: var(--cal-z-modal);
  display: flex;
  flex-direction: column;
  width: calc(100vw - var(--cal-space-8));
  max-height: calc(100vh - var(--cal-space-16));
  transform: translate(-50%, -50%);
  background: var(--cal-surface-raised);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-lg);
  box-shadow: var(--cal-shadow-xl);
  animation: cal-dialog-in var(--cal-duration-base) var(--cal-ease-out);
}

.cal-dialog.is-sm {
  max-width: 440px;
}

.cal-dialog.is-md {
  max-width: 620px;
}

.cal-dialog.is-lg {
  max-width: 860px;
}

.cal-dialog:focus {
  outline: none;
}

.cal-dialog__head {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
  padding: var(--cal-space-5) var(--cal-space-5) 0;
}

.cal-dialog__title {
  font-size: var(--cal-text-lg);
  font-weight: var(--cal-weight-semibold);
  padding-right: var(--cal-space-6);
}

.cal-dialog__description {
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
  line-height: var(--cal-leading-normal);
}

.cal-dialog__body {
  flex: 1;
  overflow-y: auto;
  padding: var(--cal-space-4) var(--cal-space-5);
}

.cal-dialog__footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-2);
  padding: var(--cal-space-4) var(--cal-space-5) var(--cal-space-5);
}

.cal-dialog__close {
  position: absolute;
  top: var(--cal-space-4);
  right: var(--cal-space-4);
  display: flex;
  padding: var(--cal-space-1);
  border: 0;
  border-radius: var(--cal-radius-xs);
  background: none;
  color: var(--cal-text-muted);
  cursor: pointer;
}

.cal-dialog__close:hover {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
}

@keyframes cal-dialog-fade {
  from {
    opacity: 0;
  }
}

@keyframes cal-dialog-in {
  from {
    opacity: 0;
    transform: translate(-50%, calc(-50% + 8px)) scale(0.98);
  }
}
</style>
