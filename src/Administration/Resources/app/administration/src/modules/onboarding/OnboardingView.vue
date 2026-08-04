<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowRight, Check } from 'lucide-vue-next'
import { workspacesApi } from '@/modules/workspaces/workspacesApi'
import { useOnboarding } from './onboarding'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalField from '@/core/ui/CalField.vue'
import CalIcon from '@/core/ui/CalIcon.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import { toast } from '@/core/feedback/toasts'

const router = useRouter()
const { steps, completedCount, isComplete, loadStatus } = useOnboarding()

const busy = ref(false)
const error = ref<string | null>(null)
const workspace = reactive({ workspaceKey: '', displayName: '', workspaceType: 'voice' })

const progress = computed(() => (steps.value.length ? (completedCount.value / steps.value.length) * 100 : 0))

async function createWorkspace(): Promise<void> {
  const key = workspace.workspaceKey.trim()
  if (!key || !workspace.displayName.trim()) {
    error.value = 'Workspace-Key und Anzeigename sind erforderlich.'
    return
  }
  busy.value = true
  error.value = null
  try {
    await workspacesApi.upsert(key, {
      displayName: workspace.displayName.trim(),
      workspaceType: workspace.workspaceType.trim() || 'voice',
      isActive: true,
      defaultSurfaceBaseUrl: null,
    })
    Object.assign(workspace, { workspaceKey: '', displayName: '', workspaceType: 'voice' })
    toast.success(`Workspace „${key}“ angelegt.`)
    await loadStatus()
  } catch (err) {
    error.value = (err as Error).message
  } finally {
    busy.value = false
  }
}

onMounted(() => void loadStatus())
</script>

<template>
  <CalPage narrow>
    <CalPageHeader
      title="Erste Schritte"
      description="Diese Schritte bringen Ihre Instanz in Betrieb. Sie können jederzeit überspringen und später fortsetzen."
    />

    <div class="onboarding__progress">
      <div class="onboarding__bar" role="progressbar" :aria-valuenow="completedCount" :aria-valuemax="steps.length">
        <div class="onboarding__bar-fill" :style="{ width: `${progress}%` }" />
      </div>
      <span class="onboarding__count">{{ completedCount }} von {{ steps.length }} erledigt</span>
    </div>

    <ol class="onboarding__steps">
      <li v-for="(step, index) in steps" :key="step.key">
        <CalCard class="onboarding__step" :class="{ 'is-done': step.done }">
          <div class="onboarding__step-head">
            <span class="onboarding__marker" :class="{ 'is-done': step.done }">
              <CalIcon v-if="step.done" :icon="Check" size="sm" />
              <template v-else>{{ index + 1 }}</template>
            </span>
            <div class="onboarding__step-copy">
              <p class="onboarding__step-label">{{ step.label }}</p>
              <p class="onboarding__step-desc">{{ step.description }}</p>
            </div>
          </div>

          <div v-if="step.key === 'workspace' && !step.done" class="onboarding__step-body">
            <CalAlert v-if="error" class="onboarding__error" tone="danger">{{ error }}</CalAlert>
            <form class="onboarding__form" @submit.prevent="createWorkspace">
              <CalField v-slot="{ id }" label="Workspace-Key" required>
                <CalInput :id="id" v-model="workspace.workspaceKey" name="workspaceKey" placeholder="hauptkanal" />
              </CalField>
              <CalField v-slot="{ id }" label="Anzeigename" required>
                <CalInput :id="id" v-model="workspace.displayName" name="displayName" placeholder="Hauptkanal" />
              </CalField>
              <CalField v-slot="{ id }" label="Typ">
                <CalInput :id="id" v-model="workspace.workspaceType" name="workspaceType" placeholder="voice" />
              </CalField>
              <CalButton type="submit" variant="primary" :loading="busy">Workspace anlegen</CalButton>
            </form>
          </div>

          <div v-else-if="!step.done" class="onboarding__step-body">
            <CalButton :to="step.to" :trailing-icon="ArrowRight">Öffnen</CalButton>
          </div>
        </CalCard>
      </li>
    </ol>

    <div class="onboarding__footer">
      <CalButton :variant="isComplete ? 'primary' : 'ghost'" to="/" :trailing-icon="ArrowRight">
        {{ isComplete ? 'Zum Dashboard' : 'Später fortsetzen' }}
      </CalButton>
    </div>
  </CalPage>
</template>

<style scoped lang="scss">
.onboarding__progress {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  margin-bottom: var(--cal-space-6);
}

.onboarding__bar {
  flex: 1;
  height: 6px;
  border-radius: var(--cal-radius-full);
  background: var(--cal-neutral-subtle);
  overflow: hidden;
}

.onboarding__bar-fill {
  height: 100%;
  border-radius: inherit;
  background: var(--cal-accent);
  transition: width var(--cal-duration-slow) var(--cal-ease-out);
}

.onboarding__count {
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
  white-space: nowrap;
}

.onboarding__steps {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-3);
}

.onboarding__step.is-done {
  opacity: 0.72;
}

.onboarding__step-head {
  display: flex;
  align-items: flex-start;
  gap: var(--cal-space-3);
}

.onboarding__marker {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  flex: none;
  border-radius: var(--cal-radius-full);
  background: var(--cal-neutral-subtle);
  color: var(--cal-text-secondary);
  font-size: var(--cal-text-sm);
  font-weight: var(--cal-weight-semibold);
}

.onboarding__marker.is-done {
  background: var(--cal-success);
  color: #fff;
}

.onboarding__step-copy {
  min-width: 0;
}

.onboarding__step-label {
  font-size: var(--cal-text-base);
  font-weight: var(--cal-weight-medium);
}

.onboarding__step-desc {
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
  line-height: var(--cal-leading-normal);
}

.onboarding__step-body {
  margin-top: var(--cal-space-4);
  padding-left: calc(24px + var(--cal-space-3));
}

.onboarding__error {
  margin-bottom: var(--cal-space-3);
}

.onboarding__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.onboarding__form > :deep(.cal-field) {
  flex: 1;
  min-width: 160px;
}

.onboarding__footer {
  margin-top: var(--cal-space-6);
}
</style>
