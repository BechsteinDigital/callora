type WorkspaceBridgeContextState = {
  workspace?: {
    key?: string;
    name?: string;
    type?: string;
  };
  route?: {
    publicBaseUrl?: string;
    publicPathPrefix?: string;
  };
};

type WorkspacePublicContextResponse = {
  workspace?: {
    key?: string;
    name?: string;
    type?: string;
  };
  route?: {
    publicBaseUrl?: string;
    publicHost?: string;
    publicPathPrefix?: string;
  };
};

export function useWorkspaceContext() {
  const config = useRuntimeConfig();
  const { requestSafe } = useWorkspaceApi();
  const bridgeContext = useState<WorkspaceBridgeContextState | null>(
    "workspace-bridge-context",
    () => null
  );

  const workspaceKey = computed(() => bridgeContext.value?.workspace?.key?.trim() || "");
  const workspaceName = computed(() =>
    bridgeContext.value?.workspace?.name?.trim() ||
    config.public.workspaceDisplayName ||
    "Callora Workspace"
  );
  const workspaceType = computed(() =>
    bridgeContext.value?.workspace?.type?.trim() ||
    config.public.workspaceType ||
    "base"
  );
  const publicPathPrefix = computed(() => bridgeContext.value?.route?.publicPathPrefix?.trim() || "/");

  async function hydrateFromPublicContext(path: string): Promise<boolean> {
    const response = await requestSafe<WorkspacePublicContextResponse>(
      `/workspace/public/context?path=${encodeURIComponent(path)}`
    );
    if (!response.ok || !response.data?.workspace?.key) {
      return false;
    }

    const nextContext: WorkspaceBridgeContextState = {
      workspace: {
        key: response.data.workspace.key || undefined,
        name: response.data.workspace.name || undefined,
        type: response.data.workspace.type || undefined
      },
      route: {
        publicBaseUrl: response.data.route?.publicBaseUrl || undefined,
        publicPathPrefix: response.data.route?.publicPathPrefix || undefined
      }
    };

    bridgeContext.value = nextContext;
    return true;
  }

  return {
    bridgeContext,
    workspaceKey,
    workspaceName,
    workspaceType,
    publicPathPrefix,
    hydrateFromPublicContext
  };
}
