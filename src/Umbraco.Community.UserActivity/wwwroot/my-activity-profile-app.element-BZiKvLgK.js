import { nothing as v, html as _, css as h, state as u, customElement as m } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as A } from "@umbraco-cms/backoffice/lit-element";
import { UmbModalToken as f, umbOpenModal as d } from "@umbraco-cms/backoffice/modal";
import { UMB_CURRENT_USER_CONTEXT as M } from "@umbraco-cms/backoffice/current-user";
import { a as b, p as C } from "./can-view.condition-s--VEVqe.js";
const E = "UserActivity.Modal.MyActivity", U = new f(E, {
  modal: { type: "sidebar", size: "full" }
});
var T = Object.defineProperty, O = Object.getOwnPropertyDescriptor, y = (t) => {
  throw TypeError(t);
}, l = (t, e, i, o) => {
  for (var r = o > 1 ? void 0 : o ? O(e, i) : e, a = t.length - 1, n; a >= 0; a--)
    (n = t[a]) && (r = (o ? n(e, i, r) : n(r)) || r);
  return o && r && T(e, i, r), r;
}, P = (t, e, i) => e.has(t) || y("Cannot " + i), w = (t, e, i) => e.has(t) ? y("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, i), K = (t, e, i) => (P(t, e, "access private method"), i), c, p;
let s = class extends A {
  constructor() {
    super(), w(this, c), this._visible = !1, b(this).then((t) => this._visible = C(t)), this.consumeContext(M, (t) => {
      t && this.observe(t.unique, (e) => this._userKey = e ?? void 0, "_userActivityCurrentUser");
    });
  }
  render() {
    return !this._visible || !this._userKey ? v : _`<uui-box headline=${this.localize.term("userActivity_myActivity")}>
      <p>${this.localize.term("userActivity_myActivityHint")}</p>
      <uui-button look="secondary" label=${this.localize.term("userActivity_openMyActivity")} @click=${K(this, c, p)}>
        <uui-icon name="icon-time"></uui-icon> ${this.localize.term("userActivity_openMyActivity")}
      </uui-button>
    </uui-box>`;
  }
};
c = /* @__PURE__ */ new WeakSet();
p = async function() {
  if (this._userKey)
    try {
      await d(this, U, { data: { userKey: this._userKey } });
    } catch {
    }
};
s.styles = h`
    p {
      margin-top: 0;
    }
  `;
l([
  u()
], s.prototype, "_userKey", 2);
l([
  u()
], s.prototype, "_visible", 2);
s = l([
  m("user-activity-my-activity-profile-app")
], s);
const L = s;
export {
  s as UserActivityMyActivityProfileAppElement,
  L as default
};
