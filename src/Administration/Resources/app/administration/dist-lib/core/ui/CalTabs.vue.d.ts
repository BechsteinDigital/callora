import type { TabItem } from './tabs';
type __VLS_Props = {
    tabs: readonly TabItem[];
};
type __VLS_PublicProps = __VLS_Props & {
    modelValue: string;
};
declare var __VLS_22: string, __VLS_23: {};
type __VLS_Slots = {} & {
    [K in NonNullable<typeof __VLS_22>]?: (props: typeof __VLS_23) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_PublicProps, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {
    "update:modelValue": (value: string) => any;
}, string, import("vue").PublicProps, Readonly<__VLS_PublicProps> & Readonly<{
    "onUpdate:modelValue"?: ((value: string) => any) | undefined;
}>, {}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalTabs.vue.d.ts.map