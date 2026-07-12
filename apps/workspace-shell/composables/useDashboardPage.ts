export function useDashboardPage() {
  const { requestSafe } = useWorkspaceApi();
  const { workspaceKey, workspaceName } = useWorkspaceContext();
  const { listResolvedWidgets } = useWorkspaceWidgets();
  const { banners } = useWorkspaceInfoBanners();

  const apiReachable = ref<boolean | null>(null);
  const loading = ref(true);
  const dashboardWidgets = listResolvedWidgets("dashboard.main");
  const blockContext = computed(() => ({ workspaceKey: workspaceKey.value }));

  async function refreshStatus(): Promise<void> {
    loading.value = true;
    try {
      const health = await requestSafe<{ status: string }>("/health");
      apiReachable.value = health.ok && (health.data?.status || "").toLowerCase() === "ok";
    } finally {
      loading.value = false;
    }
  }

  return {
    workspaceName,
    apiReachable,
    loading,
    banners,
    dashboardWidgets,
    blockContext,
    refreshStatus
  };
}
