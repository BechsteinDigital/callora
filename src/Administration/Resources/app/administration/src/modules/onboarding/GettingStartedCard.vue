<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { ArrowRight, Check, Circle, Rocket, X } from 'lucide-vue-next'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalIcon from '@/core/ui/CalIcon.vue'
import { useOnboarding } from './onboarding'

// Shown on the dashboard until the setup is complete or the operator dismisses it.
// Visibility is decided by the parent (v-if), so this only renders progress + actions.
const { steps, completedCount, loadStatus, dismiss } = useOnboarding()

const progress = computed(() => (steps.value.length ? (completedCount.value / steps.value.length) * 100 : 0))

onMounted(() => void loadStatus())
</script>

<template>
  <CalCard class="getting-started">
    <template #header>
      <div class="gs-title">
        <span class="gs-title__icon"><CalIcon :icon="Rocket" size="sm" /></span>
        <div>
          <h2 class="gs-title__text">Erste Schritte</h2>
          <p class="gs-title__progress">{{ completedCount }} von {{ steps.length }} erledigt</p>
        </div>
      </div>
    </template>

    <template #actions>
      <CalButton variant="ghost" size="sm" :icon="X" icon-only aria-label="Ausblenden" @click="dismiss" />
    </template>

    <div class="gs-bar" role="progressbar" :aria-valuenow="completedCount" :aria-valuemax="steps.length">
      <div class="gs-bar__fill" :style="{ width: `${progress}%` }" />
    </div>

    <ul class="gs-steps">
      <li v-for="step in steps" :key="step.key" class="gs-step" :class="{ 'is-done': step.done }">
        <CalIcon class="gs-step__icon" :icon="step.done ? Check : Circle" size="sm" />
        {{ step.label }}
      </li>
    </ul>

    <template #footer>
      <CalButton variant="primary" size="sm" to="/onboarding" :trailing-icon="ArrowRight">
        Setup fortsetzen
      </CalButton>
    </template>
  </CalCard>
</template>

<style scoped lang="scss">
.getting-started {
  max-width: 520px;
}

.gs-title {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
}

.gs-title__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  flex: none;
  border-radius: var(--cal-radius-sm);
  background: var(--cal-accent-subtle);
  color: var(--cal-accent);
}

.gs-title__text {
  font-size: var(--cal-text-lg);
  font-weight: var(--cal-weight-semibold);
}

.gs-title__progress {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.gs-bar {
  height: 4px;
  margin-bottom: var(--cal-space-4);
  border-radius: var(--cal-radius-full);
  background: var(--cal-neutral-subtle);
  overflow: hidden;
}

.gs-bar__fill {
  height: 100%;
  border-radius: inherit;
  background: var(--cal-accent);
  transition: width var(--cal-duration-slow) var(--cal-ease-out);
}

.gs-steps {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
}

.gs-step {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  font-size: var(--cal-text-md);
  color: var(--cal-text);
}

.gs-step__icon {
  color: var(--cal-text-muted);
}

.gs-step.is-done {
  color: var(--cal-text-muted);
  text-decoration: line-through;
  text-decoration-color: var(--cal-border-strong);
}

.gs-step.is-done .gs-step__icon {
  color: var(--cal-success);
}
</style>
