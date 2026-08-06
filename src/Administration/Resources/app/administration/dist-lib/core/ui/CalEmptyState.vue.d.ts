import type { Component } from 'vue';
/**
 * What a view shows instead of nothing. It always names the next step — an empty
 * screen without an explanation and an action is a dead end for the operator.
 */
type __VLS_Props = {
    title: string;
    description?: string;
    icon?: Component;
    compact?: boolean;
};
declare var __VLS_4: {};
type __VLS_Slots = {} & {
    action?: (props: typeof __VLS_4) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {
    compact: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalEmptyState.vue.d.ts.map