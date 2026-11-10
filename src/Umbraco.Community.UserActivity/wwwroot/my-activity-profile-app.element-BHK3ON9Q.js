import { nothing as h, html as _, css as v, state as p, customElement as m } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as f } from "@umbraco-cms/backoffice/lit-element";
import { UmbModalToken as d, umbOpenModal as A } from "@umbraco-cms/backoffice/modal";
import { UMB_CURRENT_USER_CONTEXT as b } from "@umbraco-cms/backoffice/current-user";
import { a as M, p as C } from "./can-view.condition-7gAvEVqZ.js";
import { M as E } from "./index-DsqurCFA.js";
const T = new d(E, {
  modal: { type: "sidebar", size: "full" }
});
var U = Object.defineProperty, O = Object.getOwnPropertyDescriptor, u = (t) => {
  throw TypeError(t);
}, l = (t, e, i, o) => {
  for (var s = o > 1 ? void 0 : o ? O(e, i) : e, n = t.length - 1, a; n >= 0; n--)
    (a = t[n]) && (s = (o ? a(e, i, s) : a(s)) || s);
  return o && s && U(e, i, s), s;
}, w = (t, e, i) => e.has(t) || u("Cannot " + i), x = (t, e, i) => e.has(t) ? u("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, i), P = (t, e, i) => (w(t, e, "access private method"), i), c, y;
let r = class extends f {
  constructor() {
    super(), x(this, c), this._visible = !1, this.hidden = !0, M(this).then((t) => {
      this._visible = C(t), this.hidden = !this._visible;
    }), this.consumeContext(b, (t) => {
      t && this.observe(t.unique, (e) => this._userKey = e ?? void 0, "_userActivityCurrentUser");
    });
  }
  render() {
    return !this._visible || !this._userKey ? h : _`<uui-box headline=${this.localize.term("userActivity_myActivity")}>
      <p>${this.localize.term("userActivity_myActivityHint")}</p>
      <uui-button look="secondary" label=${this.localize.term("userActivity_openMyActivity")} @click=${P(this, c, y)}>
        <uui-icon name="icon-time"></uui-icon> ${this.localize.term("userActivity_openMyActivity")}
      </uui-button>
    </uui-box>`;
  }
};
c = /* @__PURE__ */ new WeakSet();
y = async function() {
  if (this._userKey)
    try {
      await A(this, T, { data: { userKey: this._userKey } });
    } catch {
    }
};
r.styles = v`
    /* No empty slot (and no flex gap) in the profile panel for people who can't see the box. */
    :host([hidden]) {
      display: none;
    }
    p {
      margin-top: 0;
    }
  `;
l([
  p()
], r.prototype, "_userKey", 2);
l([
  p()
], r.prototype, "_visible", 2);
r = l([
  m("user-activity-my-activity-profile-app")
], r);
const S = r;
export {
  r as UserActivityMyActivityProfileAppElement,
  S as default
};
