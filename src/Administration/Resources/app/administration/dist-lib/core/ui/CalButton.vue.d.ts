import { type Component } from 'vue';
/**
 * The single button in the shell. A `to` turns it into a RouterLink that still
 * looks like a button — the pattern every list view needs for "create new",
 * which previously was a hand-styled <RouterLink> in each module.
 */
type __VLS_Props = {
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'danger-ghost';
    size?: 'sm' | 'md' | 'lg';
    type?: 'button' | 'submit' | 'reset';
    to?: string;
    icon?: Component;
    trailingIcon?: Component;
    iconOnly?: boolean;
    loading?: boolean;
    disabled?: boolean;
    block?: boolean;
};
declare var __VLS_12: {};
type __VLS_Slots = {} & {
    default?: (props: typeof __VLS_12) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {
    size: "sm" | "md" | "lg";
    type: "button" | "submit" | "reset";
    variant: "primary" | "secondary" | "ghost" | "danger" | "danger-ghost";
    iconOnly: boolean;
    loading: boolean;
    disabled: boolean;
    block: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalButton.vue.d.ts.map