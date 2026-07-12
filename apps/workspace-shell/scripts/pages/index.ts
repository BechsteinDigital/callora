export default defineComponent({
  name: "IndexPage",
  async setup() {
    const { publicPathPrefix } = useWorkspaceContext();
    const prefix = publicPathPrefix.value?.trim() || "/";
    const basePath = prefix === "/" ? "" : prefix.replace(/\/+$/, "");
    await navigateTo(`${basePath}/dashboard`, { replace: true });
    return {};
  }
});
