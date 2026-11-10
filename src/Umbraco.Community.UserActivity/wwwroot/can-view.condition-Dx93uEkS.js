import { UmbConditionBase as c } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_USER_WORKSPACE_CONTEXT as f } from "@umbraco-cms/backoffice/user";
import { UMB_CURRENT_USER_CONTEXT as m } from "@umbraco-cms/backoffice/current-user";
import { observeMultiple as l } from "@umbraco-cms/backoffice/observable-api";
import { u as p } from "./user-activity.api-DcVZJmPO.js";
const h = (t) => t?.status;
function C(t) {
  return t.data ? t.data.canReadAll ? { kind: "reader" } : { kind: "self" } : { kind: "none" };
}
function v(t, e, i) {
  return t.kind === "reader" ? !!e : t.kind === "self" ? !!e && e === i : !1;
}
function A(t) {
  let e;
  return {
    get() {
      return e ??= t().then(
        (i) => (i.data || h(i.error) === 403 || (e = void 0), C(i)),
        () => (e = void 0, { kind: "none" })
      ), e;
    }
  };
}
let o, s;
function R(t) {
  return s = t, o ??= A(() => p.filterOptions(s)), o.get();
}
class U extends c {
  constructor(e, i) {
    super(e, i), this.permitted = !1, this.consumeContext(m, (n) => {
      this.consumeContext(f, (r) => {
        if (!n || !r) {
          this.permitted = !1;
          return;
        }
        this.observe(
          l([n.unique, r.unique]),
          async ([a, u]) => {
            const d = await R(this);
            this.permitted = v(d, u ?? void 0, a ?? void 0);
          },
          "_userActivityCanView"
        );
      });
    });
  }
}
export {
  U as UserActivityCanViewCondition,
  U as api
};
