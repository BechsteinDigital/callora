/**
 * In-page message tied to the content it concerns — the replacement for the
 * `<p class="error">{{ error }}</p>` each module used to hand-roll. Transient
 * feedback about an action belongs in a toast instead.
 */
type __VLS_Props = {
    tone?: 'info' | 'success' | 'warning' | 'danger';
    title?: string;
    dismissible?: boolean;
};
declare var __VLS_4: {};
type __VLS_Slots = {} & {
    default?: (props: typeof __VLS_4) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {
    dismiss: () => any;
}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{
    onDismiss?: (() => any) | undefined;
}>, {
    tone: "info" | "success" | "warning" | "danger";
    dismissible: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalAlert.vue.d.ts.map