import type { WorkspaceInfoBanner } from "~/types/workspace-plugin-extensions";

function normalizeBanner(input: WorkspaceInfoBanner): WorkspaceInfoBanner | null {
  const id = input.id?.trim();
  const title = input.title?.trim();
  if (!id || !title) {
    return null;
  }

  return {
    id,
    pluginId: input.pluginId?.trim(),
    title,
    description: input.description?.trim()
  };
}

export function useWorkspaceInfoBanners() {
  const banners = useState<WorkspaceInfoBanner[]>("workspace-info-banners", () => []);

  function registerInfoBanner(input: WorkspaceInfoBanner): void {
    const normalized = normalizeBanner(input);
    if (!normalized) {
      return;
    }

    const filtered = banners.value.filter((entry) => entry.id !== normalized.id);
    filtered.push(normalized);
    banners.value = filtered;
  }

  return {
    banners: readonly(banners),
    registerInfoBanner
  };
}
