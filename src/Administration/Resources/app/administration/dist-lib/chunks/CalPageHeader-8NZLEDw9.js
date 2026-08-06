import { defineComponent as _, computed as p, openBlock as o, createBlock as m, resolveDynamicComponent as f, createElementBlock as n, normalizeClass as k, renderSlot as d, unref as h, withCtx as g, createVNode as v, createTextVNode as w, toDisplayString as i, createCommentVNode as l, createElementVNode as c } from "vue";
import { ArrowLeft as C } from "lucide-vue-next";
import { RouterLink as b } from "vue-router";
const y = /* @__PURE__ */ _({
  __name: "CalIcon",
  props: {
    icon: {},
    size: { default: "md" }
  },
  setup(e) {
    const a = e, t = { sm: 14, md: 16, lg: 20, xl: 24 }, s = p(() => t[a.size] ?? t.md), r = p(() => s.value >= 20 ? 1.6 : 1.75);
    return (V, E) => (o(), m(f(e.icon), {
      class: "cal-icon",
      size: s.value,
      "stroke-width": r.value,
      "aria-hidden": "true"
    }, null, 8, ["size", "stroke-width"]));
  }
}), u = (e, a) => {
  const t = e.__vccOpts || e;
  for (const [s, r] of a)
    t[s] = r;
  return t;
}, z = /* @__PURE__ */ u(y, [["__scopeId", "data-v-1806dd66"]]), x = /* @__PURE__ */ _({
  __name: "CalPage",
  props: {
    wide: { type: Boolean, default: !1 },
    narrow: { type: Boolean, default: !1 }
  },
  setup(e) {
    return (a, t) => (o(), n("div", {
      class: k(["cal-page", { "is-wide": e.wide, "is-narrow": e.narrow }])
    }, [
      d(a.$slots, "default", {}, void 0, !0)
    ], 2));
  }
}), A = /* @__PURE__ */ u(x, [["__scopeId", "data-v-065e46a7"]]), I = { class: "cal-page-header" }, $ = { class: "cal-page-header__row" }, B = { class: "cal-page-header__heading" }, S = { class: "cal-page-header__title-row" }, L = { class: "cal-page-header__title" }, N = {
  key: 0,
  class: "cal-page-header__description"
}, P = {
  key: 0,
  class: "cal-page-header__actions"
}, T = /* @__PURE__ */ _({
  __name: "CalPageHeader",
  props: {
    title: {},
    description: {},
    backTo: {},
    backLabel: { default: "Zurück" }
  },
  setup(e) {
    return (a, t) => (o(), n("header", I, [
      e.backTo ? (o(), m(h(b), {
        key: 0,
        class: "cal-page-header__back",
        to: e.backTo
      }, {
        default: g(() => [
          v(z, {
            icon: h(C),
            size: "sm"
          }, null, 8, ["icon"]),
          w(" " + i(e.backLabel), 1)
        ]),
        _: 1
      }, 8, ["to"])) : l("", !0),
      c("div", $, [
        c("div", B, [
          c("div", S, [
            c("h1", L, i(e.title), 1),
            d(a.$slots, "title-suffix", {}, void 0, !0)
          ]),
          e.description ? (o(), n("p", N, i(e.description), 1)) : l("", !0)
        ]),
        a.$slots.actions ? (o(), n("div", P, [
          d(a.$slots, "actions", {}, void 0, !0)
        ])) : l("", !0)
      ])
    ]));
  }
}), O = /* @__PURE__ */ u(T, [["__scopeId", "data-v-02ba9edd"]]);
export {
  z as C,
  u as _,
  A as a,
  O as b
};
