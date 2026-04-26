export default defineNuxtRouteMiddleware((to) => {
  const auth = useWorkspaceAuth();
  const { workspaceKey, hydrateFromPublicContext } = useWorkspaceContext();
  const dashboardPath = useRuntimeConfig().public.workspaceDashboardPath || "/dashboard";

  return (async () => {
    const isNotFoundRoute = to.path === "/404";
    const isLoginRoute = to.name === "login";
    const isPublicRoute = isLoginRoute || isNotFoundRoute;

    if (!isNotFoundRoute && workspaceKey.value.length === 0) {
      const resolved = await hydrateFromPublicContext(to.path);
      if (!resolved) {
        return navigateTo("/404");
      }
    }

    await auth.hydrate();

    const returnUrlValue = typeof to.query.returnUrl === "string" && to.query.returnUrl.trim().length > 0
      ? to.query.returnUrl
      : dashboardPath;

    if (isLoginRoute && auth.isAuthenticated.value) {
      return navigateTo(returnUrlValue);
    }

    if (!isPublicRoute && !auth.isAuthenticated.value) {
      return navigateTo({
        name: "login",
        query: {
          returnUrl: to.fullPath
        }
      });
    }

    return;
  })();
});
