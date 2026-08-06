/**
 * The masthead of every page: title, one line of orientation, and the actions
 * that belong to the page as a whole. Detail views add `backTo` to get the
 * return path that each module previously improvised.
 */
type __VLS_Props = {
    title: string;
    description?: string;
    backTo?: string;
    backLabel?: string;
};
declare var __VLS_8: {}, __VLS_10: {};
type __VLS_Slots = {} & {
    'title-suffix'?: (props: typeof __VLS_8) => any;
} & {
    actions?: (props: typeof __VLS_10) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {
    backLabel: string;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalPageHeader.vue.d.ts.map