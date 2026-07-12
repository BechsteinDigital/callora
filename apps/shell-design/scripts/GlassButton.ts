export default defineComponent({
  name: "GlassButton",
  props: {
    variant: { type: String, default: "primary" },
    type: { type: String, default: "button" },
    disabled: { type: Boolean, default: false },
    loading: { type: Boolean, default: false },
    block: { type: Boolean, default: false }
  },
  setup() {
    return {};
  }
});
