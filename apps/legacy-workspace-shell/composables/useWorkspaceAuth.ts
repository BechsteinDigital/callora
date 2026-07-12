import type {
  WorkspaceAuthMeResponse,
  WorkspaceAuthSession,
  WorkspaceLoginRequest,
  WorkspaceLoginResponse
} from "~/types/workspace-auth";

let hydrationPromise: Promise<void> | null = null;

function toSession(
  response: WorkspaceLoginResponse | WorkspaceAuthMeResponse,
  currentWorkspaceKey: string | null
): WorkspaceAuthSession {
  const loginResponse = response as WorkspaceLoginResponse;
  return {
    userId: response.userId,
    displayName: response.displayName,
    email: response.email,
    role: response.role,
    workspaceKey: loginResponse.workspaceKey ?? currentWorkspaceKey
  };
}

export function useWorkspaceAuth() {
  const runtimeConfig = useRuntimeConfig();
  const bridgeContext = useState("workspace-bridge-context", () => null as null | {
    workspace?: { key?: string };
  });
  const session = useState<WorkspaceAuthSession | null>("workspace-auth-session", () => null);
  const hydrationState = useState<"idle" | "pending" | "done">(
    "workspace-auth-hydration-state",
    () => "idle"
  );

  const isAuthenticated = computed(() => session.value !== null);

  async function hydrate(): Promise<void> {
    if (!import.meta.client || hydrationState.value === "done") {
      return;
    }

    if (hydrationState.value === "pending" && hydrationPromise) {
      await hydrationPromise;
      return;
    }

    hydrationState.value = "pending";
    hydrationPromise = (async () => {
      const workspaceKey = bridgeContext.value?.workspace?.key?.trim() || null;
      try {
        const me = await $fetch<WorkspaceAuthMeResponse>("/api/auth/me", {
          baseURL: runtimeConfig.public.calloraApiBase || undefined,
          credentials: "include"
        });
        session.value = toSession(me, workspaceKey);
      } catch {
        session.value = null;
      } finally {
        hydrationState.value = "done";
      }
    })();

    await hydrationPromise;
  }

  async function login(payload: WorkspaceLoginRequest): Promise<void> {
    const response = await $fetch<WorkspaceLoginResponse>("/workspace/auth/login", {
      method: "POST",
      baseURL: runtimeConfig.public.calloraApiBase || undefined,
      credentials: "include",
      body: payload
    });

    session.value = toSession(response, payload.workspaceKey.trim());
    hydrationState.value = "done";
  }

  async function logout(): Promise<void> {
    try {
      await $fetch("/api/auth/logout", {
        method: "POST",
        baseURL: runtimeConfig.public.calloraApiBase || undefined,
        credentials: "include"
      });
    } finally {
      session.value = null;
      hydrationState.value = "done";
    }
  }

  function invalidate(): void {
    session.value = null;
    hydrationState.value = "done";
  }

  return {
    session: readonly(session),
    isAuthenticated,
    hydrate,
    login,
    logout,
    invalidate
  };
}
