<template>
  <CalDialog
    :open="!!current"
    :title="current?.title ?? ''"
    :description="current?.description"
    @update:open="onOpenChange"
  >
    <template #footer>
      <CalButton variant="ghost" @click="answer(false)">{{ current?.cancelLabel ?? 'Abbrechen' }}</CalButton>
      <CalButton :variant="current?.tone === 'danger' ? 'danger' : 'primary'" @click="answer(true)">
        {{ current?.confirmLabel ?? 'Bestätigen' }}
      </CalButton>
    </template>
  </CalDialog>
</template>

<script setup lang="ts">
import CalButton from '@/core/ui/CalButton.vue'
import CalDialog from '@/core/ui/CalDialog.vue'
import { useConfirmDialog } from './confirm'

// Mounted once in the app shell. Every `confirm(...)` anywhere in the app
// surfaces here, so no view has to carry its own dialog markup.
const { current, answer } = useConfirmDialog()

// Escape, the close button and a click on the backdrop all mean "no".
function onOpenChange(open: boolean): void {
  if (!open) {
    answer(false)
  }
}
</script>
