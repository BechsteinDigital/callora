type __VLS_Props = {
    open: boolean;
    title: string;
    description?: string;
    size?: 'sm' | 'md' | 'lg';
};
declare var __VLS_30: {}, __VLS_32: {};
type __VLS_Slots = {} & {
    default?: (props: typeof __VLS_30) => any;
} & {
    footer?: (props: typeof __VLS_32) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {
    "update:open": (value: boolean) => any;
}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{
    "onUpdate:open"?: ((value: boolean) => any) | undefined;
}>, {
    size: "sm" | "md" | "lg";
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalDialog.vue.d.ts.map