/**
 * The list-page frame.
 *
 * Every list view built this arrangement by hand — page, header, toolbar extension slot — and
 * each one had to remember to place that slot. Here it comes WITH the pattern, so a new list gets
 * its extension point by construction rather than by discipline. The slot name follows the
 * public `{module}.list.toolbar` convention and is derived from one prop.
 *
 * Deliberately no card around the body: the views differ too much below the header (a form, a
 * table, both) for a frame to be worth imposing. The pattern owns the chrome, not the content.
 *
 * Row-level slots stay with the table that renders the rows — a frame cannot know them.
 */
type __VLS_Props = {
    /** Module segment of the slot name, e.g. 'users' → 'users.list.toolbar'. */
    module: string;
    title: string;
    description?: string;
    backTo?: string;
    /** Context handed to the toolbar extension slot. */
    ctx?: unknown;
    wide?: boolean;
    narrow?: boolean;
};
declare var __VLS_8: {}, __VLS_10: {}, __VLS_15: {};
type __VLS_Slots = {} & {
    'title-suffix'?: (props: typeof __VLS_8) => any;
} & {
    actions?: (props: typeof __VLS_10) => any;
} & {
    default?: (props: typeof __VLS_15) => any;
};
declare const __VLS_component: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {
    wide: boolean;
    narrow: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const _default: __VLS_WithSlots<typeof __VLS_component, __VLS_Slots>;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
//# sourceMappingURL=CalListPage.vue.d.ts.map