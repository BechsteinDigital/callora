<template>
  <div class="login">
    <div class="login__panel">
      <div class="login__brand">
        <span class="login__mark" aria-hidden="true">C</span>
        <span class="login__wordmark">Callora</span>
      </div>

      <div class="login__intro">
        <h1 class="login__title">Administration</h1>
        <p class="login__subtitle">Melden Sie sich mit Ihrem Betreiber- oder Workspace-Konto an.</p>
      </div>

      <form class="login__form" @submit.prevent="onSubmit">
        <CalField v-slot="{ id }" label="Login">
          <CalInput :id="id" v-model="loginName" name="login" autocomplete="username" :icon="User" />
        </CalField>

        <CalField v-slot="{ id }" label="Passwort">
          <CalInput
            :id="id"
            v-model="password"
            name="password"
            type="password"
            autocomplete="current-password"
            :icon="KeyRound"
          />
        </CalField>

        <CalField
          v-slot="{ id }"
          label="Workspace"
          hint="optional"
          description="Nur nötig für ein an einen Workspace gebundenes Konto. Betreiber lassen das Feld leer."
        >
          <CalInput :id="id" v-model="workspaceKey" name="workspaceKey" :icon="Boxes" />
        </CalField>

        <CalAlert v-if="error" tone="danger">Anmeldung fehlgeschlagen. Bitte Zugangsdaten prüfen.</CalAlert>

        <CalButton type="submit" variant="primary" size="lg" block :loading="submitting">
          {{ submitting ? 'Anmelden…' : 'Anmelden' }}
        </CalButton>
      </form>
    </div>

    <p class="login__footnote">Callora Administration</p>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { Boxes, KeyRound, User } from 'lucide-vue-next'
import { useAuthStore } from '@/core/auth/authStore'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'

const loginName = ref('')
const password = ref('')
const workspaceKey = ref('')
const error = ref(false)
const submitting = ref(false)

// Injizierbar, damit ein Test das Neuladen beobachten kann, ohne die Testumgebung zu verlassen.
const { reload = () => window.location.assign('/admin/') } = defineProps<{ reload?: () => void }>()

async function onSubmit() {
  error.value = false
  submitting.value = true
  try {
    const ok = await useAuthStore().login(loginName.value, password.value, workspaceKey.value || null)
    if (ok) {
      // Neu laden statt zu navigieren: Die Plugin-Bundles werden beim Bootstrap geladen, und
      // der lief hier ohne Sitzung — es gibt also noch keine. Sie nachzuladen ginge, ihr
      // Ergebnis aber nicht mehr rückgängig zu machen, wenn jemand danach den Workspace
      // wechselt; ein geladenes Skript lässt sich nicht entladen. Dieselbe Begründung wie bei
      // setActive: Ein Neuladen ist die einzige Antwort, die nicht halb richtig ist.
      reload()
    } else {
      error.value = true
    }
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped lang="scss">
.login {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--cal-space-6);
  min-height: 100vh;
  padding: var(--cal-space-6);
  /* A single soft accent wash keeps the sign-in screen from reading as an empty
     page, without the gradient-heavy look the rest of the shell avoids. */
  background:
    radial-gradient(ellipse 80% 60% at 50% -10%, var(--cal-accent-subtle), transparent 70%),
    var(--cal-bg);
}

.login__panel {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-6);
  width: 100%;
  max-width: 400px;
  padding: var(--cal-space-8);
  background: var(--cal-surface);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-xl);
  box-shadow: var(--cal-shadow-lg);
}

.login__brand {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}

.login__mark {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: var(--cal-radius-sm);
  background: var(--cal-accent);
  color: var(--cal-accent-contrast);
  font-weight: var(--cal-weight-bold);
}

.login__wordmark {
  font-size: var(--cal-text-lg);
  font-weight: var(--cal-weight-semibold);
  letter-spacing: -0.01em;
}

.login__intro {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-1);
}

.login__title {
  font-size: var(--cal-text-2xl);
  font-weight: var(--cal-weight-semibold);
  letter-spacing: -0.01em;
}

.login__subtitle {
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
  line-height: var(--cal-leading-normal);
}

.login__form {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-4);
}

.login__footnote {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}
</style>
