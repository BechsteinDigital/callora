<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { useOnboarding } from './onboarding'

// Shown on the dashboard until the setup is complete or the operator dismisses it.
// Visibility is decided by the parent (v-if), so this only renders progress + actions.
const { steps, completedCount, loadStatus, dismiss } = useOnboarding()

onMounted(() => void loadStatus())
</script>

<template>
  <article class="getting-started">
    <div class="gs-head">
      <strong>Erste Schritte</strong>
      <button class="gs-dismiss" title="Ausblenden" @click="dismiss">✕</button>
    </div>
    <p class="gs-progress">{{ completedCount }} / {{ steps.length }} erledigt</p>
    <ul class="gs-steps">
      <li v-for="step in steps" :key="step.key" :class="{ done: step.done }">
        <span class="gs-badge" :class="{ done: step.done }">{{ step.done ? '✓' : '•' }}</span>{{ step.label }}
      </li>
    </ul>
    <RouterLink class="gs-cta" to="/onboarding">Setup fortsetzen</RouterLink>
  </article>
</template>

<style scoped lang="scss">
.getting-started {
  border: 1px solid var(--cal-color-surface);
  border-radius: 8px;
  padding: calc(var(--cal-space) * 1.5);
  margin-bottom: calc(var(--cal-space) * 2);
  max-width: 30rem;
}

.gs-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.gs-dismiss {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--cal-color-text-muted);
}

.gs-progress {
  font-size: 0.85rem;
  color: var(--cal-color-text-muted);
  margin: 0.25rem 0;
}

.gs-steps {
  list-style: none;
  padding: 0;
  margin: 0 0 var(--cal-space);
  font-size: 0.9rem;
}

.gs-steps li {
  padding: 0.15rem 0;
}

.gs-steps li.done {
  color: var(--cal-color-text-muted);
}

.gs-badge {
  display: inline-block;
  width: 1.25rem;
  color: var(--cal-color-text-muted);
}

.gs-badge.done {
  color: var(--cal-color-success, #2e7d32);
}

.gs-cta {
  color: var(--cal-color-text);
  font-weight: 600;
}
</style>
