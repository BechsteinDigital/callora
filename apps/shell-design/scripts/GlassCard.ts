export default defineComponent({
  name: "GlassCard",
  props: {
    title: { type: String, default: undefined },
    description: { type: String, default: undefined },
    strong: { type: Boolean, default: false }
  },
  setup() {
    return {};
  }
});
