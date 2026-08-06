/**
 * Label, help text and error message around one control. The generated id is
 * exposed through the slot so the control can bind it and stay associated with
 * the label — every form in the shell gets that association for free.
 */
type __VLS_Props = {
    label: string;
    /** Short note next to the label, e.g. a unit or format. */
    hint?: string;
    /** Longer explanation under the control. Hidden while an error is shown. */
    description?: string;
    error?: string;
    required?: boolean;
    /** Label beside the control instead of above — for dense settings lists. */
    horizontal?: boolean;
    id?: string;
};
declare var __VLS_1: {
    id: string;
};
type __VLS_Slots = {} & {
    default?: (props: typeof __VLS_1) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalField.vue.d.ts.map