import { type Component } from 'vue';
import type { DataTableColumn } from './dataTable';
declare const _default: <Row extends Record<string, unknown>>(__VLS_props: NonNullable<Awaited<typeof __VLS_setup>>["props"], __VLS_ctx?: __VLS_PrettifyLocal<Pick<NonNullable<Awaited<typeof __VLS_setup>>, "attrs" | "emit" | "slots">>, __VLS_expose?: NonNullable<Awaited<typeof __VLS_setup>>["expose"], __VLS_setup?: Promise<{
    props: __VLS_PrettifyLocal<Pick<Partial<{}> & Omit<{} & import("vue").VNodeProps & import("vue").AllowedComponentProps & import("vue").ComponentCustomProps, never>, never> & {
        columns: readonly DataTableColumn[];
        rows: readonly Row[];
        /** Property holding a stable identity, or a function deriving one. */
        rowKey?: keyof Row | ((row: Row) => string);
        loading?: boolean;
        error?: string | null;
        emptyTitle?: string;
        emptyDescription?: string;
        emptyIcon?: Component;
        /** How many placeholder rows to show while loading. */
        skeletonRowCount?: number;
    } & Partial<{}>> & import("vue").PublicProps;
    expose(exposed: import("vue").ShallowUnwrapRef<{}>): void;
    attrs: any;
    slots: {
        [x: `cell-${string}`]: ((props: {
            row: Row;
            index: number;
        }) => any) | undefined;
    } & {
        'empty-action'?: (props: {}) => any;
    };
    emit: {};
}>) => import("vue").VNode & {
    __ctx?: Awaited<typeof __VLS_setup>;
};
export default _default;
type __VLS_PrettifyLocal<T> = {
    [K in keyof T]: T[K];
} & {};
//# sourceMappingURL=CalDataTable.vue.d.ts.map