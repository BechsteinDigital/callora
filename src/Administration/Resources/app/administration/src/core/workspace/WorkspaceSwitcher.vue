<template>
  <label v-if="canSwitch" class="ws-switcher">
    <span class="label">Workspace</span>
    <select :value="activeWorkspace" name="active-workspace" class="select" @change="onChange">
      <option v-for="w in workspaces" :key="w.workspaceKey" :value="w.workspaceKey">
        {{ w.displayName }}
      </option>
    </select>
  </label>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useWorkspaceContext } from './workspaceContext'

// The global active-workspace switcher, mounted in the topbar. It only renders
// for an operator who has workspaces to choose from; a workspace-bound admin has
// a fixed context and sees nothing here.
const { workspaces, activeWorkspace, canSwitch, ensure, setActive } = useWorkspaceContext()

function onChange(event: Event): void {
  setActive((event.target as HTMLSelectElement).value)
}

onMounted(ensure)
</script>

<style scoped lang="scss">
.ws-switcher {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
  color: var(--cal-color-muted);
  font-size: 0.9em;
}

.select {
  padding: calc(var(--cal-space) * 0.75) var(--cal-space);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}
</style>
