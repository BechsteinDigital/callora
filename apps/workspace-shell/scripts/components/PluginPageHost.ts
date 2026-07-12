export default defineComponent({
  name: "PluginPageHost",
  props: {
    pagePath: { type: String, required: true }
  },
  setup(props) {
    const { findPage } = useShellPages();
    const { workspaceKey } = useWorkspaceContext();

    const page = computed(() => findPage(props.pagePath));
    const blockContext = computed(() => ({ workspaceKey: workspaceKey.value }));

    return { page, blockContext };
  }
});
