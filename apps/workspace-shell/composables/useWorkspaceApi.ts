export function useWorkspaceApi() {
  const auth = useWorkspaceAuth();

  return createShellApiClient({
    async onUnauthorized() {
      auth.invalidate();

      const route = useRoute();
      if (route.name !== "login") {
        await navigateTo({
          name: "login",
          query: {
            returnUrl: route.fullPath
          }
        });
      }
    }
  });
}
