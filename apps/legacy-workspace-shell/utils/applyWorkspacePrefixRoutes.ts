import type { RouteLocationNormalized } from "vue-router";

const PREFIXED_ROUTE_NAMESPACE = "ws-prefixed:";

/**
 * Registers every page route a second time under the workspace path prefix
 * (e.g. "/test/dashboard" for "/dashboard") once the prefix is known. Returns
 * a navigation when the current target resolves to a better route afterwards.
 */
export function applyWorkspacePrefixRoutes(
  prefix: string,
  to: RouteLocationNormalized
): ReturnType<typeof navigateTo> | null {
  if (!prefix || prefix === "/") {
    return null;
  }

  const router = useRouter();
  const registered = router
    .getRoutes()
    .some((route) => String(route.name ?? "").startsWith(PREFIXED_ROUTE_NAMESPACE));

  if (!registered) {
    for (const route of [...router.getRoutes()]) {
      const name = String(route.name ?? "");
      if (!name || name.startsWith(PREFIXED_ROUTE_NAMESPACE) || name === "slug") {
        continue;
      }

      router.addRoute({
        path: route.path === "/" ? prefix : `${prefix}${route.path}`,
        name: `${PREFIXED_ROUTE_NAMESPACE}${name}`,
        components: route.components,
        meta: route.meta
      });
    }
  }

  const resolved = router.resolve(to.fullPath);
  if (resolved.name !== to.name) {
    return navigateTo(to.fullPath, { replace: true });
  }

  return null;
}
