export default defineNuxtRouteMiddleware((to) => {
  const auth = useAdminAuth();
  return (async () => {
    await auth.hydrate();

    const isLoginRoute = to.name === "login";

    if (isLoginRoute && auth.isAuthenticated.value) {
      return navigateTo({ name: "index" });
    }

    if (!isLoginRoute && !auth.isAuthenticated.value) {
      return navigateTo({ name: "login" });
    }

    return;
  })();
});
