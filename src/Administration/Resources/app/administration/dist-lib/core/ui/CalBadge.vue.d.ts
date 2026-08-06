/**
 * Status marker for tables and detail views. `tone` carries the meaning — views
 * map their domain state onto a tone rather than picking colours themselves, so
 * "active", "signed" and "healthy" read identically across modules.
 */
type __VLS_Props = {
    tone?: 'neutral' | 'accent' | 'success' | 'warning' | 'danger' | 'info';
    variant?: 'subtle' | 'outline';
    dot?: boolean;
};
declare var __VLS_1: {};
type __VLS_Slots = {} & {
    default?: (props: typeof __VLS_1) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {
    tone: "neutral" | "accent" | "success" | "warning" | "danger" | "info";
    variant: "subtle" | "outline";
    dot: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalBadge.vue.d.ts.map