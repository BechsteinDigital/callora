<script setup lang="ts">
import type { ShellBlockMountContext } from "#shell-core/types/shell-extensions";

const props = defineProps<{
  name: string;
  context?: Record<string, unknown>;
}>();

const { resolveBlock, noteBlock } = useShellBlocks();
const { translate } = useShellSnippets();
const { mountFragment } = useShellFragments();
const resolved = resolveBlock(props.name);
noteBlock(props.name);

const mountContext = computed<ShellBlockMountContext>(() => ({
  workspaceKey: "",
  ...props.context,
  blockName: props.name,
  translate,
  mountFragment
}));
</script>

<template>
  <div :data-shell-block="name">
    <ShellBlockExtensionHost
      v-for="extension in resolved.prepends"
      :key="`${extension.pluginId}:prepend`"
      :extension="extension"
      :context="mountContext"
    />

    <ShellBlockReplaceLevel
      v-if="resolved.replaceChain.length > 0"
      :levels="resolved.replaceChain"
      :index="resolved.replaceChain.length - 1"
      :context="mountContext"
    >
      <slot />
    </ShellBlockReplaceLevel>
    <slot v-else />

    <ShellBlockExtensionHost
      v-for="extension in resolved.appends"
      :key="`${extension.pluginId}:append`"
      :extension="extension"
      :context="mountContext"
    />
  </div>
</template>
