export function useAdminApi() {
  const runtimeConfig = useRuntimeConfig();
  const auth = useAdminAuth();

  async function handleUnauthorized(status: number): Promise<void> {
    if (!import.meta.client || status !== 401) {
      return;
    }

    auth.invalidate();

    const route = useRoute();
    if (route.name !== "login") {
      await navigateTo({ name: "login" });
    }
  }

  async function request<T>(path: string, init?: Parameters<typeof $fetch<T>>[1]): Promise<T> {
    try {
      return await $fetch<T>(path, {
        ...init,
        baseURL: runtimeConfig.public.calloraApiBase || undefined,
        credentials: "include"
      });
    } catch (error) {
      const status = (error as { status?: number; statusCode?: number })?.status ??
        (error as { status?: number; statusCode?: number })?.statusCode ??
        0;
      await handleUnauthorized(status);
      throw error;
    }
  }

  async function requestSafe<T>(
    path: string,
    init?: Parameters<typeof $fetch<T>>[1]
  ): Promise<{ ok: boolean; status: number; data: T | null }> {
    try {
      const response = await $fetch.raw<T>(path, {
        ...init,
        baseURL: runtimeConfig.public.calloraApiBase || undefined,
        credentials: "include",
        ignoreResponseError: true
      });

      if (response.status >= 200 && response.status < 300) {
        return {
          ok: true,
          status: response.status,
          data: response._data ?? null
        };
      }

      await handleUnauthorized(response.status);

      return {
        ok: false,
        status: response.status,
        data: null
      };
    } catch {
      return {
        ok: false,
        status: 0,
        data: null
      };
    }
  }

  return {
    request,
    requestSafe
  };
}
