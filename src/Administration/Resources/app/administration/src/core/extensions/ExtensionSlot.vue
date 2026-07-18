<template>
  <component :is="component" v-for="(component, i) in components" :key="i" :ctx="ctx" />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { getExtensions } from './registry'

// Renders every component registered for `name`, passing `ctx` through as a prop.
// Registrations are static per session (plugins register at load), so a computed
// over `name` is sufficient; empty slots render nothing.
const props = defineProps<{ name: string; ctx?: unknown }>()

const components = computed(() => getExtensions(props.name))
</script>
