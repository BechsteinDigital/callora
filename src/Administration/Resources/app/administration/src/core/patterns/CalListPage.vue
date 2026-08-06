<template>
  <CalPage :wide="wide" :narrow="narrow">
    <CalPageHeader :title="title" :description="description" :back-to="backTo">
      <template v-if="$slots['title-suffix']" #title-suffix>
        <slot name="title-suffix" />
      </template>
      <template #actions>
        <slot name="actions" />
        <ExtensionSlot :name="toolbarSlot" :ctx="ctx" />
      </template>
    </CalPageHeader>

    <slot />
  </CalPage>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'

/**
 * The list-page frame.
 *
 * Every list view built this arrangement by hand — page, header, toolbar extension slot — and
 * each one had to remember to place that slot. Here it comes WITH the pattern, so a new list gets
 * its extension point by construction rather than by discipline. The slot name follows the
 * public `{module}.list.toolbar` convention and is derived from one prop.
 *
 * Deliberately no card around the body: the views differ too much below the header (a form, a
 * table, both) for a frame to be worth imposing. The pattern owns the chrome, not the content.
 *
 * Row-level slots stay with the table that renders the rows — a frame cannot know them.
 */
const props = withDefaults(
  defineProps<{
    /** Module segment of the slot name, e.g. 'users' → 'users.list.toolbar'. */
    module: string
    title: string
    description?: string
    backTo?: string
    /** Context handed to the toolbar extension slot. */
    ctx?: unknown
    wide?: boolean
    narrow?: boolean
  }>(),
  { wide: true, narrow: false },
)

const toolbarSlot = computed(() => `${props.module}.list.toolbar`)
</script>
