import type { Component } from 'vue';
type __VLS_Props = {
    modelValue: string;
    type?: string;
    placeholder?: string;
    icon?: Component;
    size?: 'sm' | 'md';
    invalid?: boolean;
    disabled?: boolean;
};
declare var __VLS_4: {};
type __VLS_Slots = {} & {
    suffix?: (props: typeof __VLS_4) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {
    "update:modelValue": (value: string) => any;
}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{
    "onUpdate:modelValue"?: ((value: string) => any) | undefined;
}>, {
    size: "sm" | "md";
    type: string;
    disabled: boolean;
    invalid: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalInput.vue.d.ts.map