export default defineNuxtRouteMiddleware((to) => {
  const auth = useWorkspaceAuth();
  const { workspaceKey, publicPathPrefix, hydrateFromPublicContext } = useWorkspaceContext();
  const dashboardPath = useRuntimeConfig().public.workspaceDashboardPath || "/dashboard";

  return (async () => {
    const routeBaseName = String(to.name ?? "").replace(/^ws-prefixed:/, "");
    const isNotFoundRoute = to.path === "/404";
    const isLoginRoute = routeBaseName === "login";
    const isPublicRoute = isLoginRoute || isNotFoundRoute;

    if (!isNotFoundRoute && workspaceKey.value.length === 0) {
      const resolved = await hydrateFromPublicContext(to.path);
      if (!resolved) {
        return navigateTo("/404");
      }
    }

    const prefixRedirect = applyWorkspacePrefixRoutes(publicPathPrefix.value, to);
    if (prefixRedirect) {
      return prefixRedirect;
    }

    await auth.hydrate();

    const basePath = publicPathPrefix.value === "/" ? "" : publicPathPrefix.value;
    const returnUrlValue = typeof to.query.returnUrl === "string" && to.query.returnUrl.trim().length > 0
      ? to.query.returnUrl
      : `${basePath}${dashboardPath}`;

    if (isLoginRoute && auth.isAuthenticated.value) {
      return navigateTo(returnUrlValue);
    }

    if (!isPublicRoute && !auth.isAuthenticated.value) {
      return navigateTo({
        path: `${basePath}/login`,
        query: {
          returnUrl: to.fullPath
        }
      });
    }

    return;
  })();
});
