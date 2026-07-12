export interface ShellApiClientOptions {
  /**
   * Called on 401 responses on the client so the shell can invalidate its
   * session and redirect to its login route.
   */
  onUnauthorized: () => Promise<void>;
}

export interface ShellApiSafeResult<T> {
  ok: boolean;
  status: number;
  data: T | null;
}

/**
 * Shared API client for the Callora shells: cookie credentials, configurable
 * base URL and centralized 401 handling.
 */
export function createShellApiClient(options: ShellApiClientOptions) {
  const runtimeConfig = useRuntimeConfig();

  async function handleUnauthorized(status: number): Promise<void> {
    if (!import.meta.client || status !== 401) {
      return;
    }

    await options.onUnauthorized();
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
  ): Promise<ShellApiSafeResult<T>> {
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
