import { nothing as a, html as _, state as v, customElement as c } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as y } from "@umbraco-cms/backoffice/lit-element";
import { UMB_USER_WORKSPACE_CONTEXT as h } from "@umbraco-cms/backoffice/user";
import "./activity-view.element-Huo8OVpk.js";
var l = Object.defineProperty, p = Object.getOwnPropertyDescriptor, m = (e, t, o, s) => {
  for (var i = s > 1 ? void 0 : s ? p(t, o) : t, n = e.length - 1, u; n >= 0; n--)
    (u = e[n]) && (i = (s ? u(t, o, i) : u(i)) || i);
  return s && i && l(t, o, i), i;
};
let r = class extends y {
  constructor() {
    super(), this.consumeContext(h, (e) => {
      e && (this.observe(e.unique, (t) => this._userKey = t ?? void 0, "_userActivityKey"), this.observe(e.name, (t) => this._name = t ?? void 0, "_userActivityName"));
    });
  }
  render() {
    if (!this._userKey) return a;
    const e = this._name ? this.localize.term("userActivity_activityFor", this._name) : this.localize.term("userActivity_activity");
    return _`<user-activity-view user-key=${this._userKey} heading=${e}></user-activity-view>`;
  }
};
m([
  v()
], r.prototype, "_userKey", 2);
m([
  v()
], r.prototype, "_name", 2);
r = m([
  c("user-activity-user-tab")
], r);
const U = r;
export {
  r as UserActivityUserTabElement,
  U as default
};
