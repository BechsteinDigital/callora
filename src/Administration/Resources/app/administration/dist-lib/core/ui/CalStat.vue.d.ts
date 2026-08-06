import { type Component } from 'vue';
/**
 * A single headline figure on the dashboard. It distinguishes "still loading"
 * from "could not be read" — an operator must never mistake a failed metric for
 * a genuine zero.
 */
type __VLS_Props = {
    label: string;
    value?: number | string | null;
    caption?: string;
    icon?: Component;
    loading?: boolean;
    /** The value could not be read; renders a dash in muted colour. */
    unavailable?: boolean;
    /** Makes the whole tile a link to the list behind the figure. */
    to?: string;
};
declare const _default: import("vue").DefineComponent<__VLS_Props, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<__VLS_Props> & Readonly<{}>, {}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
export default _default;
//# sourceMappingURL=CalStat.vue.d.ts.map