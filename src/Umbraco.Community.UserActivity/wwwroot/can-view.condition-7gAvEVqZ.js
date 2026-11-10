import { UmbConditionBase as f } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_USER_WORKSPACE_CONTEXT as l } from "@umbraco-cms/backoffice/user";
import { UMB_CURRENT_USER_CONTEXT as m } from "@umbraco-cms/backoffice/current-user";
import { observeMultiple as p } from "@umbraco-cms/backoffice/observable-api";
import { u as _ } from "./user-activity.api-DcVZJmPO.js";
const C = (t) => t?.status;
function h(t) {
  return t.data ? t.data.canReadAll ? { kind: "reader" } : { kind: "self" } : { kind: "none" };
}
function v(t, e, n) {
  return t.kind === "reader" ? !!e : t.kind === "self" ? !!e && e === n : !1;
}
function g(t) {
  return t.kind !== "none";
}
function b(t) {
  let e;
  return {
    get(n) {
      return e ??= t(n).then(
        (i) => (i.data || C(i.error) === 403 || (e = void 0), h(i)),
        () => (e = void 0, { kind: "none" })
      ), e;
    }
  };
}
let o;
function a(t) {
  return o ??= b((e) => _.filterOptions(e)), o.get(t);
}
class s extends f {
  constructor(e, n) {
    super(e, n), this.permitted = !1, this.consumeContext(m, (i) => {
      this.consumeContext(l, (r) => {
        if (!i || !r) {
          this.permitted = !1;
          return;
        }
        this.observe(
          p([i.unique, r.unique]),
          async ([u, d]) => {
            const c = await a(this);
            this.permitted = v(c, d ?? void 0, u ?? void 0);
          },
          "_userActivityCanView"
        );
      });
    });
  }
}
const S = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  UserActivityCanViewCondition: s,
  access: a,
  api: s
}, Symbol.toStringTag, { value: "Module" }));
export {
  a,
  S as c,
  g as p
};
