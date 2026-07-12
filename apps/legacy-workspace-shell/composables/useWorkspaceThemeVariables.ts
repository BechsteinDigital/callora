type WorkspacePublicThemeResponse = {
  themePluginId?: string | null;
  valuesByKey?: Record<string, string>;
};

function toCssVariableName(settingKey: string): string {
  const normalized = settingKey
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return normalized ? `--callora-${normalized}` : "";
}

export function useWorkspaceThemeVariables() {
  const runtimeConfig = useRuntimeConfig();
  const { workspaceKey } = useWorkspaceContext();
  const applied = useState<boolean>("workspace-theme-variables-applied", () => false);

  async function applyWorkspaceThemeVariables(): Promise<void> {
    if (!import.meta.client || applied.value) {
      return;
    }

    try {
      const response = await $fetch<WorkspacePublicThemeResponse>(
        `/workspace/public/theme?workspaceKey=${encodeURIComponent(workspaceKey.value || "default")}`,
        {
          baseURL: runtimeConfig.public.calloraApiBase || undefined,
          credentials: "include"
        }
      );

      const values = response.valuesByKey ?? {};
      for (const [settingKey, value] of Object.entries(values)) {
        const variableName = toCssVariableName(settingKey);
        if (variableName && typeof value === "string") {
          // setProperty assigns exactly one declaration; invalid values are
          // dropped by the CSSOM, so theme values cannot inject further rules.
          document.documentElement.style.setProperty(variableName, value);
        }
      }
    } catch {
      // Without theme values the shell keeps its built-in styling.
    } finally {
      applied.value = true;
    }
  }

  return {
    applyWorkspaceThemeVariables
  };
}
