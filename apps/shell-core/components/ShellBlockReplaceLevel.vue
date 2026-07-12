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

async function mountLevel(): Promise<void> {
  if (typeof cleanup === "function") {
    cleanup();
    cleanup = undefined;
  }
  parentTarget.value = null;

  if (level.value?.mount && host.value) {
    cleanup = level.value.mount(host.value, props.context);
  }

  await nextTick();
  parentTarget.value = host.value?.querySelector<HTMLElement>("[data-shell-parent]") ?? null;
}

onMounted(() => {
  void mountLevel();
  // Re-mount when a later-registered replace extension shifts this level.
  watch(level, () => {
    void mountLevel();
  });
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
    <component :is="level.component" v-if="level?.component" :context="context" />
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
