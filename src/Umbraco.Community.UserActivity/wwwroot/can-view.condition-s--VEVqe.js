import { UmbConditionBase as l } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_USER_WORKSPACE_CONTEXT as m } from "@umbraco-cms/backoffice/user";
import { UMB_CURRENT_USER_CONTEXT as p } from "@umbraco-cms/backoffice/current-user";
import { observeMultiple as _ } from "@umbraco-cms/backoffice/observable-api";
import { u as h } from "./user-activity.api-DcVZJmPO.js";
const C = (t) => t?.status;
function v(t) {
  return t.data ? t.data.canReadAll ? { kind: "reader" } : { kind: "self" } : { kind: "none" };
}
function b(t, e, i) {
  return t.kind === "reader" ? !!e : t.kind === "self" ? !!e && e === i : !1;
}
function S(t) {
  return t.kind !== "none";
}
function A(t) {
  let e;
  return {
    get() {
      return e ??= t().then(
        (i) => (i.data || C(i.error) === 403 || (e = void 0), v(i)),
        () => (e = void 0, { kind: "none" })
      ), e;
    }
  };
}
let o, s;
function u(t) {
  return s = t, o ??= A(() => h.filterOptions(s)), o.get();
}
class a extends l {
  constructor(e, i) {
    super(e, i), this.permitted = !1, this.consumeContext(p, (n) => {
      this.consumeContext(m, (r) => {
        if (!n || !r) {
          this.permitted = !1;
          return;
        }
        this.observe(
          _([n.unique, r.unique]),
          async ([c, d]) => {
            const f = await u(this);
            this.permitted = b(f, d ?? void 0, c ?? void 0);
          },
          "_userActivityCanView"
        );
      });
    });
  }
}
const U = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  UserActivityCanViewCondition: a,
  access: u,
  api: a
}, Symbol.toStringTag, { value: "Module" }));
export {
  u as a,
  U as c,
  S as p
};
