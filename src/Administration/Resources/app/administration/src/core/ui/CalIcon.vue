<template>
  <component :is="icon" class="cal-icon" :size="pixelSize" :stroke-width="strokeWidth" aria-hidden="true" />
</template>

<script setup lang="ts">
import { computed, type Component } from 'vue'

// Single place where icon geometry is decided. Views pass a lucide component and
// a semantic size, never raw pixels — that keeps stroke weight and optical size
// consistent across the shell, including icons a plugin contributes.
const props = withDefaults(defineProps<{ icon: Component; size?: 'sm' | 'md' | 'lg' | 'xl' }>(), {
  size: 'md',
})

const SIZES: Record<string, number> = { sm: 14, md: 16, lg: 20, xl: 24 }

const pixelSize = computed(() => SIZES[props.size] ?? SIZES.md)
// Larger glyphs need a thinner stroke to keep the same visual weight.
const strokeWidth = computed(() => (pixelSize.value >= 20 ? 1.6 : 1.75))
</script>

<style scoped lang="scss">
.cal-icon {
  flex: none;
  display: block;
}
</style>
