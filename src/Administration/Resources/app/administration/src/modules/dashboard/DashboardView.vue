<template>
  <section class="dashboard">
    <h1>Übersicht</h1>
    <dl v-if="ctx">
      <dt>Benutzer</dt>
      <dd>{{ ctx.displayName ?? ctx.userId }} ({{ ctx.userId }})</dd>
      <dt>Scope</dt>
      <dd>{{ ctx.scope ?? '—' }}{{ ctx.workspaceKey ? ` / ${ctx.workspaceKey}` : '' }}</dd>
      <dt>Rollen</dt>
      <dd>{{ ctx.roles.join(', ') || '—' }}</dd>
      <dt>Operator</dt>
      <dd>{{ ctx.isOperator ? 'ja' : 'nein' }}</dd>
      <dt>Permissions</dt>
      <dd>{{ ctx.permissions.length }}</dd>
    </dl>
  </section>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/core/auth/authStore'

const ctx = useAuthStore().context
</script>

<style scoped lang="scss">
.dashboard {
  padding: calc(var(--cal-space) * 3);
}

dl {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: var(--cal-space) calc(var(--cal-space) * 2);
  max-width: 480px;
}

dt {
  color: var(--cal-color-muted);
}
</style>
