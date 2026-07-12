export function useAdminApi() {
  const auth = useAdminAuth();

  return createShellApiClient({
    async onUnauthorized() {
      auth.invalidate();

      const route = useRoute();
      if (route.name !== "login") {
        await navigateTo({ name: "login" });
      }
    }
  });
}
