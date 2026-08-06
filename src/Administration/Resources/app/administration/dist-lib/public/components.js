import { defineComponent as f, computed as w, openBlock as t, createElementBlock as l, normalizeClass as r, createVNode as b, createElementVNode as n, toDisplayString as c, createCommentVNode as o, renderSlot as u, unref as h, createBlock as y, resolveDynamicComponent as D, withCtx as _, mergeProps as S, normalizeStyle as T, createTextVNode as z, Fragment as g, renderList as p, createSlots as L, useId as R, useModel as E, mergeModels as N } from "vue";
import { XCircle as q, AlertTriangle as P, CheckCircle2 as F, Info as M, X as A, Check as U, ChevronDown as X } from "lucide-vue-next";
import { C as k, _ as m } from "../chunks/CalPageHeader-8NZLEDw9.js";
import { a as Nt, b as qt } from "../chunks/CalPageHeader-8NZLEDw9.js";
import { RouterLink as O } from "vue-router";
import { DialogRoot as H, DialogPortal as j, DialogOverlay as x, DialogContent as G, DialogTitle as J, DialogDescription as Q, DialogClose as W, TabsRoot as Y, TabsList as Z, TabsTrigger as ee, TabsContent as te } from "radix-vue";
const ae = ["role"], le = { class: "cal-alert__body" }, se = {
  key: 0,
  class: "cal-alert__title"
}, ie = { class: "cal-alert__text" }, oe = /* @__PURE__ */ f({
  __name: "CalAlert",
  props: {
    tone: { default: "info" },
    title: {},
    dismissible: { type: Boolean, default: !1 }
  },
  emits: ["dismiss"],
  setup(e) {
    const a = e, s = {
      info: M,
      success: F,
      warning: P,
      danger: q
    }, i = w(() => s[a.tone]);
    return (d, I) => (t(), l("div", {
      class: r(["cal-alert", `is-${e.tone}`]),
      role: e.tone === "danger" ? "alert" : "status"
    }, [
      b(k, {
        class: "cal-alert__icon",
        icon: i.value,
        size: "sm"
      }, null, 8, ["icon"]),
      n("div", le, [
        e.title ? (t(), l("p", se, c(e.title), 1)) : o("", !0),
        n("div", ie, [
          u(d.$slots, "default", {}, void 0, !0)
        ])
      ]),
      e.dismissible ? (t(), l("button", {
        key: 0,
        type: "button",
        class: "cal-alert__close",
        "aria-label": "Schließen",
        onClick: I[0] || (I[0] = ($) => d.$emit("dismiss"))
      }, [
        b(k, {
          icon: h(A),
          size: "sm"
        }, null, 8, ["icon"])
      ])) : o("", !0)
    ], 10, ae));
  }
}), ne = /* @__PURE__ */ m(oe, [["__scopeId", "data-v-89e2f7c1"]]), de = {
  key: 0,
  class: "cal-badge__dot"
}, ce = /* @__PURE__ */ f({
  __name: "CalBadge",
  props: {
    tone: { default: "neutral" },
    variant: { default: "subtle" },
    dot: { type: Boolean, default: !1 }
  },
  setup(e) {
    return (a, s) => (t(), l("span", {
      class: r(["cal-badge", [`is-${e.tone}`, `is-${e.variant}`]])
    }, [
      e.dot ? (t(), l("span", de)) : o("", !0),
      u(a.$slots, "default", {}, void 0, !0)
    ], 2));
  }
}), Ct = /* @__PURE__ */ m(ce, [["__scopeId", "data-v-7fb70163"]]), ue = ["aria-label"], re = /* @__PURE__ */ f({
  __name: "CalSpinner",
  props: {
    size: { default: "md" },
    label: { default: "Lädt" }
  },
  setup(e) {
    return (a, s) => (t(), l("span", {
      class: r(["cal-spinner", `is-${e.size}`]),
      role: "status",
      "aria-label": e.label
    }, null, 10, ue));
  }
}), fe = /* @__PURE__ */ m(re, [["__scopeId", "data-v-0d2d75fd"]]), me = {
  key: 2,
  class: "cal-btn__label"
}, he = /* @__PURE__ */ f({
  __name: "CalButton",
  props: {
    variant: { default: "secondary" },
    size: { default: "md" },
    type: { default: "button" },
    to: {},
    icon: {},
    trailingIcon: {},
    iconOnly: { type: Boolean, default: !1 },
    loading: { type: Boolean, default: !1 },
    disabled: { type: Boolean, default: !1 },
    block: { type: Boolean, default: !1 }
  },
  setup(e) {
    const a = e, s = w(() => a.to ? O : "button"), i = w(() => a.to ? void 0 : a.disabled || a.loading);
    return (d, I) => (t(), y(D(s.value), {
      to: e.to,
      type: e.to ? void 0 : e.type,
      disabled: i.value,
      "aria-disabled": i.value || void 0,
      "aria-busy": e.loading || void 0,
      class: r(["cal-btn", [`is-${e.variant}`, `is-${e.size}`, { "is-block": e.block, "is-icon-only": e.iconOnly, "is-loading": e.loading }]])
    }, {
      default: _(() => [
        e.loading ? (t(), y(fe, {
          key: 0,
          class: "cal-btn__spinner",
          size: e.size === "lg" ? "md" : "sm"
        }, null, 8, ["size"])) : e.icon ? (t(), y(k, {
          key: 1,
          icon: e.icon,
          size: e.size === "lg" ? "lg" : "sm"
        }, null, 8, ["icon", "size"])) : o("", !0),
        e.iconOnly ? o("", !0) : (t(), l("span", me, [
          u(d.$slots, "default", {}, void 0, !0)
        ])),
        e.trailingIcon && !e.iconOnly ? (t(), y(k, {
          key: 3,
          icon: e.trailingIcon,
          size: e.size === "lg" ? "lg" : "sm"
        }, null, 8, ["icon", "size"])) : o("", !0)
      ]),
      _: 3
    }, 8, ["to", "type", "disabled", "aria-disabled", "aria-busy", "class"]));
  }
}), pt = /* @__PURE__ */ m(he, [["__scopeId", "data-v-7c4ca52f"]]), ye = {
  key: 0,
  class: "cal-card__head"
}, ve = { class: "cal-card__heading" }, _e = {
  key: 0,
  class: "cal-card__title"
}, be = {
  key: 1,
  class: "cal-card__description"
}, $e = {
  key: 0,
  class: "cal-card__actions"
}, ke = { class: "cal-card__body" }, ge = {
  key: 1,
  class: "cal-card__footer"
}, Ce = /* @__PURE__ */ f({
  __name: "CalCard",
  props: {
    title: {},
    description: {},
    padding: { default: "md" },
    flush: { type: Boolean, default: !1 }
  },
  setup(e) {
    return (a, s) => (t(), l("section", {
      class: r(["cal-card", [`is-${e.padding}`, { "is-flush": e.flush }]])
    }, [
      e.title || a.$slots.actions || a.$slots.header ? (t(), l("header", ye, [
        n("div", ve, [
          e.title ? (t(), l("h2", _e, c(e.title), 1)) : o("", !0),
          e.description ? (t(), l("p", be, c(e.description), 1)) : o("", !0),
          u(a.$slots, "header", {}, void 0, !0)
        ]),
        a.$slots.actions ? (t(), l("div", $e, [
          u(a.$slots, "actions", {}, void 0, !0)
        ])) : o("", !0)
      ])) : o("", !0),
      n("div", ke, [
        u(a.$slots, "default", {}, void 0, !0)
      ]),
      a.$slots.footer ? (t(), l("footer", ge, [
        u(a.$slots, "footer", {}, void 0, !0)
      ])) : o("", !0)
    ], 2));
  }
}), wt = /* @__PURE__ */ m(Ce, [["__scopeId", "data-v-99bbf9f9"]]), pe = ["checked", "disabled"], we = { class: "cal-checkbox__box" }, ze = {
  key: 0,
  class: "cal-checkbox__label"
}, Ie = /* @__PURE__ */ f({
  inheritAttrs: !1,
  __name: "CalCheckbox",
  props: {
    modelValue: { type: Boolean },
    disabled: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue"],
  setup(e) {
    return (a, s) => (t(), l("label", {
      class: r(["cal-checkbox", { "is-disabled": e.disabled }])
    }, [
      n("input", S(a.$attrs, {
        type: "checkbox",
        class: "cal-checkbox__input",
        checked: e.modelValue,
        disabled: e.disabled,
        onChange: s[0] || (s[0] = (i) => a.$emit("update:modelValue", i.target.checked))
      }), null, 16, pe),
      n("span", we, [
        e.modelValue ? (t(), y(k, {
          key: 0,
          icon: h(U),
          size: "sm"
        }, null, 8, ["icon"])) : o("", !0)
      ]),
      a.$slots.default ? (t(), l("span", ze, [
        u(a.$slots, "default", {}, void 0, !0)
      ])) : o("", !0)
    ], 2));
  }
}), zt = /* @__PURE__ */ m(Ie, [["__scopeId", "data-v-09c59ec9"]]), Be = {
  key: 0,
  class: "cal-empty__icon"
}, Ve = { class: "cal-empty__title" }, Se = {
  key: 1,
  class: "cal-empty__description"
}, De = {
  key: 2,
  class: "cal-empty__action"
}, Te = /* @__PURE__ */ f({
  __name: "CalEmptyState",
  props: {
    title: {},
    description: {},
    icon: {},
    compact: { type: Boolean, default: !1 }
  },
  setup(e) {
    return (a, s) => (t(), l("div", {
      class: r(["cal-empty", { "is-compact": e.compact }])
    }, [
      e.icon ? (t(), l("div", Be, [
        b(k, {
          icon: e.icon,
          size: "lg"
        }, null, 8, ["icon"])
      ])) : o("", !0),
      n("p", Ve, c(e.title), 1),
      e.description ? (t(), l("p", Se, c(e.description), 1)) : o("", !0),
      a.$slots.action ? (t(), l("div", De, [
        u(a.$slots, "action", {}, void 0, !0)
      ])) : o("", !0)
    ], 2));
  }
}), Ae = /* @__PURE__ */ m(Te, [["__scopeId", "data-v-f1cd00e6"]]), Oe = /* @__PURE__ */ f({
  __name: "CalSkeleton",
  props: {
    width: { default: "100%" },
    height: { default: "12px" }
  },
  setup(e) {
    return (a, s) => (t(), l("span", {
      class: "cal-skeleton",
      style: T({ width: e.width, height: e.height }),
      "aria-hidden": "true"
    }, null, 4));
  }
}), K = /* @__PURE__ */ m(Oe, [["__scopeId", "data-v-e63cfeff"]]), Ke = { class: "cal-table" }, Le = { class: "cal-table__scroll" }, Re = { class: "cal-table__grid" }, Ee = { key: 0 }, Ne = { key: 1 }, qe = /* @__PURE__ */ f({
  __name: "CalDataTable",
  props: {
    columns: {},
    rows: {},
    rowKey: {},
    loading: { type: Boolean, default: !1 },
    error: { default: null },
    emptyTitle: { default: "Keine Einträge vorhanden." },
    emptyDescription: {},
    emptyIcon: {},
    skeletonRowCount: { default: 4 }
  },
  setup(e) {
    const a = e, s = w(() => a.columns.filter(($) => !$.hidden)), i = w(() => Array.from({ length: a.skeletonRowCount }, ($, B) => B));
    function d($, B) {
      return typeof a.rowKey == "function" ? a.rowKey($) : a.rowKey ? String($[a.rowKey]) : B;
    }
    function I($, B) {
      const v = $[B];
      return v == null || v === "" ? "—" : String(v);
    }
    return ($, B) => (t(), l("div", Ke, [
      e.error ? (t(), y(ne, {
        key: 0,
        tone: "danger",
        class: "cal-table__error"
      }, {
        default: _(() => [
          z(c(e.error), 1)
        ]),
        _: 1
      })) : o("", !0),
      n("div", Le, [
        n("table", Re, [
          n("thead", null, [
            n("tr", null, [
              (t(!0), l(g, null, p(s.value, (v) => (t(), l("th", {
                key: v.key,
                style: T({ width: v.width }),
                class: r({ "is-end": v.align === "end" }),
                scope: "col"
              }, c(v.label), 7))), 128))
            ])
          ]),
          e.loading ? (t(), l("tbody", Ee, [
            (t(!0), l(g, null, p(i.value, (v) => (t(), l("tr", {
              key: `skeleton-${v}`,
              class: "cal-table__skeleton-row"
            }, [
              (t(!0), l(g, null, p(s.value, (V) => {
                var C;
                return t(), l("td", {
                  key: V.key
                }, [
                  b(K, {
                    width: V.key === ((C = s.value[0]) == null ? void 0 : C.key) ? "55%" : "75%"
                  }, null, 8, ["width"])
                ]);
              }), 128))
            ]))), 128))
          ])) : e.rows.length ? (t(), l("tbody", Ne, [
            (t(!0), l(g, null, p(e.rows, (v, V) => (t(), l("tr", {
              key: d(v, V)
            }, [
              (t(!0), l(g, null, p(s.value, (C) => (t(), l("td", {
                key: C.key,
                class: r({ "is-end": C.align === "end", "is-mono": C.mono })
              }, [
                u($.$slots, `cell-${C.key}`, {
                  row: v,
                  index: V
                }, () => [
                  z(c(I(v, C.key)), 1)
                ], !0)
              ], 2))), 128))
            ]))), 128))
          ])) : o("", !0)
        ])
      ]),
      !e.loading && !e.rows.length && !e.error ? (t(), y(Ae, {
        key: 1,
        class: "cal-table__empty",
        compact: "",
        title: e.emptyTitle,
        description: e.emptyDescription,
        icon: e.emptyIcon
      }, L({ _: 2 }, [
        $.$slots["empty-action"] ? {
          name: "action",
          fn: _(() => [
            u($.$slots, "empty-action", {}, void 0, !0)
          ]),
          key: "0"
        } : void 0
      ]), 1032, ["title", "description", "icon"])) : o("", !0)
    ]));
  }
}), It = /* @__PURE__ */ m(qe, [["__scopeId", "data-v-c0efa9cb"]]), Pe = { class: "cal-dl__term" }, Fe = /* @__PURE__ */ f({
  __name: "CalDescriptionList",
  props: {
    items: {},
    stacked: { type: Boolean, default: !1 }
  },
  setup(e) {
    return (a, s) => (t(), l("dl", {
      class: r(["cal-dl", { "is-stacked": e.stacked }])
    }, [
      (t(!0), l(g, null, p(e.items, (i) => (t(), l(g, {
        key: i.term
      }, [
        n("dt", Pe, c(i.term), 1),
        n("dd", {
          class: r(["cal-dl__value", { "is-mono": i.mono }])
        }, [
          u(a.$slots, i.term, { item: i }, () => [
            z(c(i.value || "—"), 1)
          ], !0)
        ], 2)
      ], 64))), 128))
    ], 2));
  }
}), Bt = /* @__PURE__ */ m(Fe, [["__scopeId", "data-v-609c8b27"]]), Me = { class: "cal-dialog__head" }, Ue = {
  key: 0,
  class: "cal-dialog__body"
}, Xe = {
  key: 1,
  class: "cal-dialog__footer"
}, He = /* @__PURE__ */ f({
  __name: "CalDialog",
  props: {
    open: { type: Boolean },
    title: {},
    description: {},
    size: { default: "sm" }
  },
  emits: ["update:open"],
  setup(e) {
    return (a, s) => (t(), y(h(H), {
      open: e.open,
      "onUpdate:open": s[0] || (s[0] = (i) => a.$emit("update:open", i))
    }, {
      default: _(() => [
        b(h(j), null, {
          default: _(() => [
            b(h(x), { class: "cal-dialog__overlay" }),
            b(h(G), {
              class: r(["cal-dialog", `is-${e.size}`])
            }, {
              default: _(() => [
                n("header", Me, [
                  b(h(J), { class: "cal-dialog__title" }, {
                    default: _(() => [
                      z(c(e.title), 1)
                    ]),
                    _: 1
                  }),
                  e.description ? (t(), y(h(Q), {
                    key: 0,
                    class: "cal-dialog__description"
                  }, {
                    default: _(() => [
                      z(c(e.description), 1)
                    ]),
                    _: 1
                  })) : o("", !0)
                ]),
                a.$slots.default ? (t(), l("div", Ue, [
                  u(a.$slots, "default", {}, void 0, !0)
                ])) : o("", !0),
                a.$slots.footer ? (t(), l("footer", Xe, [
                  u(a.$slots, "footer", {}, void 0, !0)
                ])) : o("", !0),
                b(h(W), {
                  class: "cal-dialog__close",
                  "aria-label": "Schließen"
                }, {
                  default: _(() => [
                    b(k, {
                      icon: h(A),
                      size: "sm"
                    }, null, 8, ["icon"])
                  ]),
                  _: 1
                })
              ]),
              _: 3
            }, 8, ["class"])
          ]),
          _: 3
        })
      ]),
      _: 3
    }, 8, ["open"]));
  }
}), Vt = /* @__PURE__ */ m(He, [["__scopeId", "data-v-7d4d1b60"]]), je = { class: "cal-field__head" }, xe = ["for"], Ge = {
  key: 0,
  class: "cal-field__required",
  "aria-hidden": "true"
}, Je = {
  key: 0,
  class: "cal-field__hint"
}, Qe = { class: "cal-field__control" }, We = {
  key: 0,
  class: "cal-field__error"
}, Ye = {
  key: 1,
  class: "cal-field__description"
}, Ze = /* @__PURE__ */ f({
  __name: "CalField",
  props: {
    label: {},
    hint: {},
    description: {},
    error: {},
    required: { type: Boolean },
    horizontal: { type: Boolean },
    id: {}
  },
  setup(e) {
    const a = e, s = R(), i = w(() => a.id ?? s);
    return (d, I) => (t(), l("div", {
      class: r(["cal-field", { "is-horizontal": e.horizontal }])
    }, [
      n("div", je, [
        n("label", {
          class: "cal-field__label",
          for: i.value
        }, [
          z(c(e.label) + " ", 1),
          e.required ? (t(), l("span", Ge, "*")) : o("", !0)
        ], 8, xe),
        e.hint && !e.error ? (t(), l("span", Je, c(e.hint), 1)) : o("", !0)
      ]),
      n("div", Qe, [
        u(d.$slots, "default", { id: i.value }, void 0, !0),
        e.error ? (t(), l("p", We, c(e.error), 1)) : e.description ? (t(), l("p", Ye, c(e.description), 1)) : o("", !0)
      ])
    ], 2));
  }
}), St = /* @__PURE__ */ m(Ze, [["__scopeId", "data-v-33d0a12a"]]), et = ["value", "type", "disabled", "placeholder", "aria-invalid"], tt = {
  key: 1,
  class: "cal-input__suffix"
}, at = /* @__PURE__ */ f({
  inheritAttrs: !1,
  __name: "CalInput",
  props: {
    modelValue: {},
    type: { default: "text" },
    placeholder: {},
    icon: {},
    size: { default: "md" },
    invalid: { type: Boolean, default: !1 },
    disabled: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue"],
  setup(e) {
    return (a, s) => (t(), l("div", {
      class: r(["cal-input", [`is-${e.size}`, { "is-invalid": e.invalid, "is-disabled": e.disabled, "has-icon": e.icon }]])
    }, [
      e.icon ? (t(), y(k, {
        key: 0,
        class: "cal-input__icon",
        icon: e.icon,
        size: "sm"
      }, null, 8, ["icon"])) : o("", !0),
      n("input", S(a.$attrs, {
        class: "cal-input__field",
        value: e.modelValue,
        type: e.type,
        disabled: e.disabled,
        placeholder: e.placeholder,
        "aria-invalid": e.invalid || void 0,
        onInput: s[0] || (s[0] = (i) => a.$emit("update:modelValue", i.target.value))
      }), null, 16, et),
      a.$slots.suffix ? (t(), l("span", tt, [
        u(a.$slots, "suffix", {}, void 0, !0)
      ])) : o("", !0)
    ], 2));
  }
}), Dt = /* @__PURE__ */ m(at, [["__scopeId", "data-v-efd1353d"]]), lt = ["value", "disabled"], st = /* @__PURE__ */ f({
  inheritAttrs: !1,
  __name: "CalSelect",
  props: {
    modelValue: {},
    size: { default: "md" },
    disabled: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue"],
  setup(e) {
    return (a, s) => (t(), l("div", {
      class: r(["cal-select", [`is-${e.size}`, { "is-disabled": e.disabled }]])
    }, [
      n("select", S(a.$attrs, {
        class: "cal-select__field",
        value: e.modelValue,
        disabled: e.disabled,
        onChange: s[0] || (s[0] = (i) => a.$emit("update:modelValue", i.target.value))
      }), [
        u(a.$slots, "default", {}, void 0, !0)
      ], 16, lt),
      b(k, {
        class: "cal-select__chevron",
        icon: h(X),
        size: "sm"
      }, null, 8, ["icon"])
    ], 2));
  }
}), Tt = /* @__PURE__ */ m(st, [["__scopeId", "data-v-e59397c8"]]), it = { class: "cal-stat__head" }, ot = { class: "cal-stat__label" }, nt = {
  key: 0,
  class: "cal-stat__icon"
}, dt = {
  key: 2,
  class: "cal-stat__caption"
}, ct = /* @__PURE__ */ f({
  __name: "CalStat",
  props: {
    label: {},
    value: {},
    caption: {},
    icon: {},
    loading: { type: Boolean },
    unavailable: { type: Boolean },
    to: {}
  },
  setup(e) {
    const a = e, s = w(() => a.unavailable ? "—" : a.value ?? "—");
    return (i, d) => (t(), l("article", {
      class: r(["cal-stat", { "is-linked": !!e.to }])
    }, [
      (t(), y(D(e.to ? h(O) : "div"), {
        to: e.to,
        class: "cal-stat__inner"
      }, {
        default: _(() => [
          n("div", it, [
            n("span", ot, c(e.label), 1),
            e.icon ? (t(), l("span", nt, [
              b(k, {
                icon: e.icon,
                size: "sm"
              }, null, 8, ["icon"])
            ])) : o("", !0)
          ]),
          e.loading ? (t(), y(K, {
            key: 0,
            class: "cal-stat__loading",
            width: "42%",
            height: "26px"
          })) : (t(), l("span", {
            key: 1,
            class: r(["cal-stat__value", { "is-unavailable": e.unavailable }])
          }, c(s.value), 3)),
          e.caption ? (t(), l("span", dt, c(e.caption), 1)) : o("", !0)
        ]),
        _: 1
      }, 8, ["to"]))
    ], 2));
  }
}), At = /* @__PURE__ */ m(ct, [["__scopeId", "data-v-4e002c66"]]), ut = ["checked", "disabled"], rt = {
  key: 0,
  class: "cal-switch__label"
}, ft = /* @__PURE__ */ f({
  inheritAttrs: !1,
  __name: "CalSwitch",
  props: {
    modelValue: { type: Boolean },
    disabled: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue"],
  setup(e) {
    return (a, s) => (t(), l("label", {
      class: r(["cal-switch", { "is-disabled": e.disabled }])
    }, [
      n("input", S(a.$attrs, {
        type: "checkbox",
        role: "switch",
        class: "cal-switch__input",
        checked: e.modelValue,
        disabled: e.disabled,
        onChange: s[0] || (s[0] = (i) => a.$emit("update:modelValue", i.target.checked))
      }), null, 16, ut),
      s[1] || (s[1] = n("span", { class: "cal-switch__track" }, [
        n("span", { class: "cal-switch__thumb" })
      ], -1)),
      a.$slots.default ? (t(), l("span", rt, [
        u(a.$slots, "default", {}, void 0, !0)
      ])) : o("", !0)
    ], 2));
  }
}), Ot = /* @__PURE__ */ m(ft, [["__scopeId", "data-v-442c223b"]]), mt = {
  key: 1,
  class: "cal-tabs__count"
}, ht = /* @__PURE__ */ f({
  __name: "CalTabs",
  props: /* @__PURE__ */ N({
    tabs: {}
  }, {
    modelValue: { required: !0 },
    modelModifiers: {}
  }),
  emits: ["update:modelValue"],
  setup(e) {
    const a = E(e, "modelValue");
    return (s, i) => (t(), y(h(Y), {
      modelValue: a.value,
      "onUpdate:modelValue": i[0] || (i[0] = (d) => a.value = d),
      class: "cal-tabs"
    }, {
      default: _(() => [
        b(h(Z), { class: "cal-tabs__list" }, {
          default: _(() => [
            (t(!0), l(g, null, p(e.tabs, (d) => (t(), y(h(ee), {
              key: d.value,
              class: "cal-tabs__trigger",
              value: d.value
            }, {
              default: _(() => [
                d.icon ? (t(), y(k, {
                  key: 0,
                  icon: d.icon,
                  size: "sm"
                }, null, 8, ["icon"])) : o("", !0),
                z(" " + c(d.label) + " ", 1),
                d.count !== void 0 ? (t(), l("span", mt, c(d.count), 1)) : o("", !0)
              ]),
              _: 2
            }, 1032, ["value"]))), 128))
          ]),
          _: 1
        }),
        (t(!0), l(g, null, p(e.tabs, (d) => (t(), y(h(te), {
          key: d.value,
          class: "cal-tabs__content",
          value: d.value
        }, {
          default: _(() => [
            u(s.$slots, d.value, {}, void 0, !0)
          ]),
          _: 2
        }, 1032, ["value"]))), 128))
      ]),
      _: 3
    }, 8, ["modelValue"]));
  }
}), Kt = /* @__PURE__ */ m(ht, [["__scopeId", "data-v-45088561"]]), yt = ["value", "rows", "disabled", "placeholder", "aria-invalid"], vt = /* @__PURE__ */ f({
  __name: "CalTextarea",
  props: {
    modelValue: {},
    rows: { default: 4 },
    placeholder: {},
    mono: { type: Boolean, default: !1 },
    invalid: { type: Boolean, default: !1 },
    disabled: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue"],
  setup(e) {
    return (a, s) => (t(), l("textarea", S(a.$attrs, {
      class: ["cal-textarea", { "is-invalid": e.invalid, "is-mono": e.mono }],
      value: e.modelValue,
      rows: e.rows,
      disabled: e.disabled,
      placeholder: e.placeholder,
      "aria-invalid": e.invalid || void 0,
      onInput: s[0] || (s[0] = (i) => a.$emit("update:modelValue", i.target.value))
    }), null, 16, yt));
  }
}), Lt = /* @__PURE__ */ m(vt, [["__scopeId", "data-v-73a750e4"]]);
export {
  ne as CalAlert,
  Ct as CalBadge,
  pt as CalButton,
  wt as CalCard,
  zt as CalCheckbox,
  It as CalDataTable,
  Bt as CalDescriptionList,
  Vt as CalDialog,
  Ae as CalEmptyState,
  St as CalField,
  k as CalIcon,
  Dt as CalInput,
  Nt as CalPage,
  qt as CalPageHeader,
  Tt as CalSelect,
  K as CalSkeleton,
  fe as CalSpinner,
  At as CalStat,
  Ot as CalSwitch,
  Kt as CalTabs,
  Lt as CalTextarea
};
