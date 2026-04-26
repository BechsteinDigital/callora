<script setup lang="ts">
import * as z from 'zod';
import type { FormSubmitEvent, AuthFormField } from '@nuxt/ui';

definePageMeta({
  layout: false
});

const auth = useAdminAuth();

const isLoading = ref(false);
const errorMessage = ref<string | null>(null);

const fields: AuthFormField[] = [{
  name: 'login',
  type: 'text',
  label: 'Login',
  placeholder: 'admin@callora.local',
  required: true
}, {
  name: 'password',
  label: 'Password',
  type: 'password',
  placeholder: 'Enter your password',
  required: true
}];

const schema = z.object({
  login: z.string().min(1, 'Login is required'),
  password: z.string().min(1, 'Password is required')
});

type Schema = z.output<typeof schema>;

async function onSubmit(payload: FormSubmitEvent<Schema>): Promise<void> {
  errorMessage.value = null;
  isLoading.value = true;

  try {
    await auth.login({
      login: payload.data.login.trim(),
      password: payload.data.password
    });

    await navigateTo({ name: 'index' });
  } catch {
    errorMessage.value = 'Authentication failed. Check your credentials.';
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <main class="min-h-svh flex items-center justify-center p-4">
    <UPageCard class="w-full max-w-md">
      <UAuthForm
        :schema="schema"
        title="Admin Login"
        description="Sign in to access the Callora admin dashboard."
        icon="i-lucide-shield"
        :fields="fields"
        :disabled="isLoading"
        :loading="isLoading"
        @submit="onSubmit"
      />

      <UAlert
        v-if="errorMessage"
        class="mt-4"
        color="error"
        variant="soft"
        :description="errorMessage"
        icon="i-lucide-triangle-alert"
      />
    </UPageCard>
  </main>
</template>
