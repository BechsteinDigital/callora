import { defineComponent as i, computed as c, openBlock as l, createElementBlock as x, Fragment as p, renderList as k, createBlock as u, resolveDynamicComponent as w, withCtx as a, createVNode as s, createSlots as b, renderSlot as r } from "vue";
import { a as g, b as C } from "../chunks/CalPageHeader-8NZLEDw9.js";
const $ = [];
function y(t) {
  return $.filter((e) => e.slot === t).sort((e, o) => e.order - o.order).map((e) => e.component);
}
const B = /* @__PURE__ */ i({
  __name: "ExtensionSlot",
  props: {
    name: {},
    ctx: {}
  },
  setup(t) {
    const e = t, o = c(() => y(e.name));
    return (n, m) => (l(!0), x(p, null, k(o.value, (d, f) => (l(), u(w(d), {
      key: f,
      ctx: t.ctx
    }, null, 8, ["ctx"]))), 128));
  }
}), E = /* @__PURE__ */ i({
  __name: "CalListPage",
  props: {
    module: {},
    title: {},
    description: {},
    backTo: {},
    ctx: {},
    wide: { type: Boolean, default: !0 },
    narrow: { type: Boolean, default: !1 }
  },
  setup(t) {
    const e = t, o = c(() => `${e.module}.list.toolbar`);
    return (n, m) => (l(), u(g, {
      wide: t.wide,
      narrow: t.narrow
    }, {
      default: a(() => [
        s(C, {
          title: t.title,
          description: t.description,
          "back-to": t.backTo
        }, b({
          actions: a(() => [
            r(n.$slots, "actions"),
            s(B, {
              name: o.value,
              ctx: t.ctx
            }, null, 8, ["name", "ctx"])
          ]),
          _: 2
        }, [
          n.$slots["title-suffix"] ? {
            name: "title-suffix",
            fn: a(() => [
              r(n.$slots, "title-suffix")
            ]),
            key: "0"
          } : void 0
        ]), 1032, ["title", "description", "back-to"]),
        r(n.$slots, "default")
      ]),
      _: 3
    }, 8, ["wide", "narrow"]));
  }
});
export {
  E as CalListPage,
  B as ExtensionSlot
};
