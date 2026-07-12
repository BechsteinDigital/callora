<script setup lang="ts">
import * as z from "zod";
import type { AuthFormField, FormSubmitEvent } from "@nuxt/ui";

definePageMeta({
  layout: false
});

const route = useRoute();
const auth = useWorkspaceAuth();
const { workspaceKey, workspaceName, hydrateFromPublicContext } = useWorkspaceContext();

const isLoading = ref(false);
const isResolvingWorkspace = ref(false);
const errorMessage = ref<string | null>(null);

const fields: AuthFormField[] = [{
  name: "login",
  type: "text",
  label: "Login",
  placeholder: "agent@callora.local",
  required: true
}, {
  name: "password",
  label: "Password",
  type: "password",
  placeholder: "Enter your password",
  required: true
}];

const schema = z.object({
  login: z.string().min(1, "Login is required"),
  password: z.string().min(1, "Password is required")
});

type Schema = z.output<typeof schema>;

async function resolveWorkspaceKey(): Promise<string> {
  if (workspaceKey.value.length > 0) {
    return workspaceKey.value;
  }

  const candidatePath = typeof route.query.returnUrl === "string" && route.query.returnUrl.trim().length > 0
    ? route.query.returnUrl
    : "/";
  const resolved = await hydrateFromPublicContext(candidatePath);
  if (!resolved) {
    return "";
  }

  return workspaceKey.value;
}

async function preloadWorkspaceContext(): Promise<void> {
  if (workspaceKey.value.length > 0) {
    return;
  }

  isResolvingWorkspace.value = true;
  try {
    await resolveWorkspaceKey();
  } finally {
    isResolvingWorkspace.value = false;
  }
}

async function onSubmit(payload: FormSubmitEvent<Schema>): Promise<void> {
  errorMessage.value = null;
  isLoading.value = true;

  try {
    const workspaceKey = await resolveWorkspaceKey();
    if (!workspaceKey) {
      errorMessage.value = "No workspace context found for this login.";
      return;
    }

    await auth.login({
      login: payload.data.login.trim(),
      password: payload.data.password,
      workspaceKey
    });

    const returnUrl = typeof route.query.returnUrl === "string" && route.query.returnUrl.trim().length > 0
      ? route.query.returnUrl
      : (useRuntimeConfig().public.workspaceDashboardPath || "/dashboard");
    await navigateTo(returnUrl);
  } catch {
    errorMessage.value = "Authentication failed. Check your credentials and workspace assignment.";
  } finally {
    isLoading.value = false;
  }
}

onMounted(async () => {
  await preloadWorkspaceContext();
});
</script>

<template>
  <main class="min-h-svh flex items-center justify-center p-4 bg-muted/20">
    <UPageCard class="w-full max-w-md">
      <UAuthForm
        :schema="schema"
        title="Workspace Login"
        :description="`Sign in to workspace ${workspaceName}.`"
        icon="i-lucide-layout-grid"
        :fields="fields"
        :disabled="isLoading || isResolvingWorkspace || !workspaceKey"
        :loading="isLoading || isResolvingWorkspace"
        @submit="onSubmit"
      />

      <UAlert
        v-if="workspaceKey"
        class="mt-4"
        color="neutral"
        variant="soft"
        icon="i-lucide-building-2"
        :description="`Workspace key: ${workspaceKey}`"
      />

      <UAlert
        v-else-if="isResolvingWorkspace"
        class="mt-4"
        color="neutral"
        variant="soft"
        icon="i-lucide-loader-circle"
        description="Resolving workspace context..."
      />

      <UAlert
        v-else
        class="mt-4"
        color="warning"
        variant="soft"
        icon="i-lucide-circle-alert"
        description="No workspace was resolved for this URL. Check workspace public route mapping."
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
