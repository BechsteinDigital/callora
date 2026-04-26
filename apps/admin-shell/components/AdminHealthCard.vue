<script setup lang="ts">
type BackendUserSummary = {
  externalId: string;
  email: string | null;
  displayName: string | null;
};

const runtimeConfig = useRuntimeConfig();
const { request } = useAdminApi();

const status = ref<"idle" | "pending" | "success" | "error">("idle");
const error = ref<string | null>(null);
const users = ref<BackendUserSummary[]>([]);

const baseUrl = computed(() => runtimeConfig.public.calloraApiBase || window.location.origin);
const usersUrl = computed(() => `${baseUrl.value}/api/users`);

async function refresh(): Promise<void> {
  status.value = "pending";
  error.value = null;

  try {
    const response = await request<BackendUserSummary[]>("/api/users");
    users.value = response;
    status.value = "success";
  } catch {
    status.value = "error";
    error.value = "Admin-API nicht erreichbar oder Zugriff verweigert.";
  }
}

await refresh();
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-center gap-2">
      <UBadge
        :color="error ? 'error' : 'success'"
        variant="soft"
        :icon="error ? 'i-lucide-circle-x' : 'i-lucide-circle-check'"
      >
        {{ error ? 'Disconnected' : 'Connected' }}
      </UBadge>

      <UBadge
        color="neutral"
        variant="subtle"
        icon="i-lucide-users"
      >
        {{ users.length }} Users
      </UBadge>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      :description="error"
      icon="i-lucide-triangle-alert"
    />

    <div class="flex flex-wrap items-center gap-3">
      <UButton
        color="neutral"
        variant="outline"
        icon="i-lucide-refresh-cw"
        :loading="status === 'pending'"
        @click="refresh"
      >
        Refresh API
      </UButton>

      <span class="text-xs text-muted">{{ usersUrl }}</span>
    </div>
  </div>
</template>
