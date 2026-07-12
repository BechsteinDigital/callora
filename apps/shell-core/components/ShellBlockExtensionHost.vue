<script setup lang="ts">
import type { ShellBlockExtensionEntry, ShellBlockMountContext } from "#shell-core/types/shell-extensions";

const props = defineProps<{
  extension: ShellBlockExtensionEntry;
  context: ShellBlockMountContext;
}>();

const container = ref<HTMLElement | null>(null);
let cleanup: (() => void) | void;

onMounted(() => {
  if (props.extension.mount && container.value) {
    cleanup = props.extension.mount(container.value, props.context);
  }
});

onBeforeUnmount(() => {
  if (typeof cleanup === "function") {
    cleanup();
  }
});
</script>

<template>
  <div :data-shell-block-plugin="extension.pluginId">
    <!-- Plugin block content is trusted shell-extension code by design;
         plugins already execute JS via their UI bundles. -->
    <div v-if="extension.contentHtml" v-html="extension.contentHtml" />
    <component :is="extension.component" v-if="extension.component" :context="context" />
    <div v-if="extension.mount" ref="container" />
  </div>
</template>
