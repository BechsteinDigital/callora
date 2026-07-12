<script setup lang="ts">
import { h, resolveComponent } from "vue";
import type { TableColumn } from "@nuxt/ui";

interface BackgroundJobRow {
  id: string;
  jobType: string;
  status: string;
  workspaceKey: string | null;
  attemptCount: number;
  maxAttempts: number;
  scheduledAtUtc: string;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  lastError: string | null;
}

const { requestSafe } = useAdminApi();

const jobs = ref<BackgroundJobRow[]>([]);
const loading = ref(true);
const loadError = ref<string | null>(null);
const statusFilter = ref<string>("all");

const statusFilterItems = [
  { label: "Alle Status", value: "all" },
  { label: "Pending", value: "Pending" },
  { label: "Running", value: "Running" },
  { label: "Succeeded", value: "Succeeded" },
  { label: "Failed (Dead Letter)", value: "Failed" }
];

const filteredJobs = computed(() => {
  if (statusFilter.value === "all") {
    return jobs.value;
  }

  return jobs.value.filter((job) => job.status === statusFilter.value);
});

const failedCount = computed(() => jobs.value.filter((job) => job.status === "Failed").length);

function toDateTime(value: string | null): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
}

function statusColor(status: string): "success" | "error" | "info" | "warning" | "neutral" {
  switch (status) {
    case "Succeeded":
      return "success";
    case "Failed":
      return "error";
    case "Running":
      return "info";
    case "Pending":
      return "warning";
    default:
      return "neutral";
  }
}

async function loadJobs(): Promise<void> {
  loading.value = true;
  loadError.value = null;

  const response = await requestSafe<BackgroundJobRow[]>("/api/jobs");
  if (!response.ok) {
    jobs.value = [];
    loadError.value = "Jobs konnten nicht geladen werden.";
    loading.value = false;
    return;
  }

  jobs.value = response.data ?? [];
  loading.value = false;
}

const tableColumns: TableColumn<BackgroundJobRow>[] = [
  {
    id: "jobType",
    accessorKey: "jobType",
    header: "Job-Typ"
  },
  {
    id: "status",
    accessorKey: "status",
    header: "Status",
    cell: ({ row }) => h(resolveComponent("UBadge"), {
      color: statusColor(row.original.status),
      variant: "subtle"
    }, () => row.original.status)
  },
  {
    id: "attempts",
    header: "Versuche",
    cell: ({ row }) => `${row.original.attemptCount}/${row.original.maxAttempts}`
  },
  {
    id: "workspaceKey",
    accessorKey: "workspaceKey",
    header: "Workspace",
    cell: ({ row }) => row.original.workspaceKey || "-"
  },
  {
    id: "scheduledAtUtc",
    header: "Geplant",
    cell: ({ row }) => toDateTime(row.original.scheduledAtUtc)
  },
  {
    id: "completedAtUtc",
    header: "Abgeschlossen",
    cell: ({ row }) => toDateTime(row.original.completedAtUtc)
  },
  {
    id: "lastError",
    accessorKey: "lastError",
    header: "Letzter Fehler",
    cell: ({ row }) => row.original.lastError || "-"
  }
];

await loadJobs();
</script>

<template>
  <UDashboardPanel id="jobs">
    <template #header>
      <UDashboardNavbar title="Background Jobs">
        <template #right>
          <USelect
            v-model="statusFilter"
            :items="statusFilterItems"
            class="w-52"
          />
          <UButton
            icon="i-lucide-refresh-cw"
            color="neutral"
            variant="soft"
            :loading="loading"
            @click="loadJobs"
          >
            Aktualisieren
          </UButton>
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UAlert
        v-if="failedCount > 0"
        class="mb-4"
        color="error"
        variant="soft"
        :title="`${failedCount} Job(s) im Dead-Letter-Zustand`"
        description="Fehlgeschlagene Jobs haben ihr Retry-Budget ausgeschöpft. Details in der Spalte 'Letzter Fehler'."
      />

      <UAlert
        v-if="loadError"
        class="mb-4"
        color="error"
        variant="soft"
        :description="loadError"
      />

      <UPageCard>
        <UTable
          :loading="loading"
          :columns="tableColumns"
          :data="filteredJobs"
          empty="Keine Jobs vorhanden."
        />
      </UPageCard>
    </template>
  </UDashboardPanel>
</template>
