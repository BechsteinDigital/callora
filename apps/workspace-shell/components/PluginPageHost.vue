<template>
  <div v-if="page" class="plugin-page">
    <ShellBlock :name="`workspace.${page.pageId}.before`" :context="blockContext" />
    <!-- The page content itself is just a block: the owning plugin registers
         a replace extension with priority 0, so templates can override or
         wrap it (parent()) like any other block. -->
    <ShellBlock :name="`workspace.${page.pageId}.content`" :context="blockContext">
      <GlassCard
        title="Seite nicht verfügbar"
        description="Die Erweiterung, die diese Seite bereitstellt, ist nicht geladen."
      />
    </ShellBlock>
    <ShellBlock :name="`workspace.${page.pageId}.after`" :context="blockContext" />
  </div>
</template>

<script lang="ts" src="../scripts/components/PluginPageHost.ts"></script>

<style lang="scss" src="./PluginPageHost.scss" scoped></style>
