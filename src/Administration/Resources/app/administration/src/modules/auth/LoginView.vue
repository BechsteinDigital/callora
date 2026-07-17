<template>
  <form class="login" @submit.prevent="onSubmit">
    <h1>Callora Administration</h1>
    <label>Login <input name="login" v-model="loginName" /></label>
    <label>Passwort <input name="password" type="password" v-model="password" /></label>
    <label>Workspace (optional) <input name="workspaceKey" v-model="workspaceKey" /></label>
    <p v-if="error" class="error">Anmeldung fehlgeschlagen.</p>
    <BaseButton type="submit">Anmelden</BaseButton>
  </form>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'
import BaseButton from '@/core/ui/BaseButton.vue'

const loginName = ref('')
const password = ref('')
const workspaceKey = ref('')
const error = ref(false)
const router = useRouter()

async function onSubmit() {
  error.value = false
  const ok = await useAuthStore().login(loginName.value, password.value, workspaceKey.value || null)
  if (ok) {
    router.push('/')
  } else {
    error.value = true
  }
}
</script>

<style scoped lang="scss">
.login {
  max-width: 360px;
  margin: 10vh auto;
  display: flex;
  flex-direction: column;
  gap: var(--cal-space);
}

.login label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
