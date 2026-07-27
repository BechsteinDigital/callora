<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { workspacesApi } from '@/modules/workspaces/workspacesApi'
import { useOnboarding } from './onboarding'

const router = useRouter()
const { steps, completedCount, isComplete, loadStatus } = useOnboarding()

const busy = ref(false)
const error = ref<string | null>(null)
const workspace = reactive({ workspaceKey: '', displayName: '', workspaceType: 'voice' })

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
      publicBaseUrl: null,
    })
    Object.assign(workspace, { workspaceKey: '', displayName: '', workspaceType: 'voice' })
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
  <section class="onboarding">
    <header>
      <h1>Erste Schritte</h1>
      <p class="lead">
        Willkommen bei Callora. Diese Schritte bringen deine Instanz in Betrieb — du kannst
        jederzeit überspringen und später fortsetzen.
      </p>
      <div class="progress">{{ completedCount }} / {{ steps.length }} erledigt</div>
    </header>

    <ol class="steps">
      <li v-for="step in steps" :key="step.key" class="step" :class="{ done: step.done }">
        <div class="step-head">
          <span class="badge" :class="{ done: step.done }">{{ step.done ? '✓' : '•' }}</span>
          <div>
            <div class="step-label">{{ step.label }}</div>
            <div class="step-desc">{{ step.description }}</div>
          </div>
        </div>

        <div v-if="step.key === 'workspace' && !step.done" class="step-body">
          <p v-if="error" class="error">{{ error }}</p>
          <form class="ws-form" @submit.prevent="createWorkspace">
            <label>Workspace-Key<input v-model="workspace.workspaceKey" placeholder="z. B. hauptkanal" /></label>
            <label>Anzeigename<input v-model="workspace.displayName" placeholder="Hauptkanal" /></label>
            <label>Typ<input v-model="workspace.workspaceType" placeholder="voice" /></label>
            <button type="submit" :disabled="busy">Workspace anlegen</button>
          </form>
        </div>
        <div v-else-if="!step.done" class="step-body">
          <RouterLink class="action" :to="step.to">Öffnen</RouterLink>
        </div>
      </li>
    </ol>

    <footer>
      <RouterLink class="action" to="/">{{ isComplete ? 'Zum Dashboard' : 'Später fortsetzen' }}</RouterLink>
    </footer>
  </section>
</template>

<style scoped lang="scss">
.onboarding {
  padding: calc(var(--cal-space) * 2);
  max-width: 46rem;
}

.lead {
  color: var(--cal-color-text-muted);
  max-width: 60ch;
}

.progress {
  margin-top: var(--cal-space);
  font-weight: 600;
}

.steps {
  list-style: none;
  padding: 0;
  margin: calc(var(--cal-space) * 2) 0;
  display: flex;
  flex-direction: column;
  gap: var(--cal-space);
}

.step {
  border: 1px solid var(--cal-color-surface);
  border-radius: 8px;
  padding: calc(var(--cal-space) * 1.5);
}

.step-head {
  display: flex;
  gap: var(--cal-space);
  align-items: flex-start;
}

.badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.5rem;
  height: 1.5rem;
  border-radius: 50%;
  background: var(--cal-color-surface);
  font-weight: 700;
}

.badge.done {
  background: var(--cal-color-success, #2e7d32);
  color: #fff;
}

.step-desc {
  color: var(--cal-color-text-muted);
  font-size: 0.9rem;
}

.step-body {
  margin-top: var(--cal-space);
  padding-left: calc(1.5rem + var(--cal-space));
}

.ws-form {
  display: grid;
  grid-template-columns: repeat(3, 1fr) auto;
  gap: 0.5rem;
  align-items: end;
}

.ws-form label {
  display: flex;
  flex-direction: column;
  font-size: 0.85rem;
  gap: 0.15rem;
}

.action {
  display: inline-block;
  color: var(--cal-color-text);
}

.error {
  color: var(--cal-color-danger, #c0392b);
}
</style>
