export default defineComponent({
  name: "GlassInput",
  props: {
    modelValue: { type: String, default: "" },
    label: { type: String, default: undefined },
    type: { type: String, default: "text" },
    placeholder: { type: String, default: undefined },
    autocomplete: { type: String, default: undefined },
    hint: { type: String, default: undefined }
  },
  emits: ["update:modelValue"],
  setup(props, { emit }) {
    const model = computed({
      get: () => props.modelValue,
      set: (value: string) => emit("update:modelValue", value)
    });

    return { model };
  }
});
