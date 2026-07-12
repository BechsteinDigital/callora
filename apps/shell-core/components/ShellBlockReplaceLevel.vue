<script setup lang="ts">
import type { ShellBlockExtensionEntry, ShellBlockMountContext } from "#shell-core/types/shell-extensions";

const props = defineProps<{
  levels: ShellBlockExtensionEntry[];
  index: number;
  context: ShellBlockMountContext;
}>();

const level = computed(() => props.levels[props.index]);
const host = ref<HTMLElement | null>(null);
const parentTarget = ref<HTMLElement | null>(null);
let cleanup: (() => void) | void;

onMounted(async () => {
  if (level.value?.mount && host.value) {
    cleanup = level.value.mount(host.value, props.context);
  }

  await nextTick();
  parentTarget.value = host.value?.querySelector<HTMLElement>("[data-shell-parent]") ?? null;
});

onBeforeUnmount(() => {
  if (typeof cleanup === "function") {
    cleanup();
  }
});
</script>

<template>
  <div ref="host" :data-shell-block-plugin="level?.pluginId">
    <!-- Plugin block content is trusted shell-extension code by design;
         plugins already execute JS via their UI bundles. -->
    <div v-if="level?.contentHtml" v-html="level.contentHtml" />
  </div>

  <Teleport v-if="parentTarget" :to="parentTarget">
    <ShellBlockReplaceLevel
      v-if="index > 0"
      :levels="levels"
      :index="index - 1"
      :context="context"
    >
      <slot />
    </ShellBlockReplaceLevel>
    <slot v-else />
  </Teleport>
</template>
