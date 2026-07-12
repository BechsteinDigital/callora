import type { AdminAuthMeResponse, AdminAuthSession, AdminLoginRequest, AdminLoginResponse } from "~/types/admin-auth";

let hydrationPromise: Promise<void> | null = null;

function toSession(response: AdminLoginResponse | AdminAuthMeResponse): AdminAuthSession {
  return {
    userId: response.userId,
    displayName: response.displayName,
    email: response.email,
    role: response.role
  };
}

export function useAdminAuth() {
  const runtimeConfig = useRuntimeConfig();
  const session = useState<AdminAuthSession | null>("admin-auth-session", () => null);
  const hydrationState = useState<"idle" | "pending" | "done">(
    "admin-auth-hydration-state",
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
      try {
        const me = await $fetch<AdminAuthMeResponse>("/api/auth/me", {
          baseURL: runtimeConfig.public.calloraApiBase || undefined,
          credentials: "include"
        });
        session.value = toSession(me);
      } catch {
        session.value = null;
      } finally {
        hydrationState.value = "done";
      }
    })();

    await hydrationPromise;
  }

  async function login(payload: AdminLoginRequest): Promise<void> {
    const response = await $fetch<AdminLoginResponse>("/api/auth/login", {
      method: "POST",
      baseURL: runtimeConfig.public.calloraApiBase || undefined,
      credentials: "include",
      body: payload
    });

    session.value = toSession(response);
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
