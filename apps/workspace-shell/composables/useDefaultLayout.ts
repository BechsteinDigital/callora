export function useDefaultLayout() {
  const auth = useWorkspaceAuth();
  const { workspaceKey, workspaceName, workspaceType, publicPathPrefix } = useWorkspaceContext();
  const { pages: pluginPages } = useShellPages();

  const workspaceBasePath = computed(() => {
    const prefix = publicPathPrefix.value?.trim() || "/";
    return prefix === "/" ? "" : prefix.replace(/\/+$/, "");
  });

  function toWorkspacePath(relativePath: string): string {
    const normalized = relativePath.startsWith("/") ? relativePath : `/${relativePath}`;
    return `${workspaceBasePath.value}${normalized}` || "/";
  }

  const navigationLinks = computed(() => [
    { label: "Übersicht", to: toWorkspacePath("/dashboard"), icon: "🏠" },
    ...[...pluginPages.value]
      .sort((left, right) =>
        (left.navOrder ?? 100) - (right.navOrder ?? 100) || left.title.localeCompare(right.title))
      .map((page) => ({
        label: page.title,
        to: toWorkspacePath(page.path),
        icon: page.icon || "🧩"
      }))
  ]);

  const userLabel = computed(() =>
    auth.session.value?.displayName ||
    auth.session.value?.email ||
    "Angemeldet"
  );

  async function logout(): Promise<void> {
    await auth.logout();
    await navigateTo(toWorkspacePath("/login"));
  }

  const blockContext = computed(() => ({ workspaceKey: workspaceKey.value }));

  return {
    workspaceKey,
    workspaceName,
    workspaceType,
    navigationLinks,
    userLabel,
    logout,
    blockContext
  };
}
