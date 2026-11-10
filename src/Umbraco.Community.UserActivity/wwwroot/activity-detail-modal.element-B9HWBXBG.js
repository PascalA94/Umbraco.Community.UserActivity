var w = (i) => {
  throw TypeError(i);
};
var D = (i, a, t) => a.has(i) || w("Cannot " + t);
var x = (i, a, t) => a.has(i) ? w("Cannot add the same private member more than once") : a instanceof WeakSet ? a.add(i) : a.set(i, t);
var s = (i, a, t) => (D(i, a, "access private method"), t);
import { LitElement as K, nothing as n, html as c, css as z, state as O, customElement as C } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement as M } from "@umbraco-cms/backoffice/modal";
import { u as N } from "./user-activity.api-DcVZJmPO.js";
import { i as k, t as U, a as P, f as L, b as _, m as J } from "./format-CI07-hff.js";
function j(i) {
  const a = i.trim();
  if (!(!a.startsWith("{") && !a.startsWith("[")))
    try {
      return JSON.stringify(JSON.parse(a), null, 2);
    } catch {
      return;
    }
}
var e, r, o;
const p = class p extends K {
  constructor() {
    super();
    x(this, e);
    this.event = void 0, this.localize = k, this.locale = void 0, this.sourceKeys = {};
  }
  render() {
    const t = this.event;
    if (!t) return n;
    const l = t.actor.via, d = t.actor.kind === "defaultAdmin" || l?.kind === "defaultAdmin", u = Object.entries(t.details ?? {}), h = P(t.entity), I = Object.hasOwn(this.sourceKeys, t.source) ? s(this, e, r).call(this, this.sourceKeys[t.source], t.source) : t.source;
    return c`
      <uui-box data-section="what" headline=${s(this, e, r).call(this, "userActivity_detailWhat", "What")}>
        <dl>
          ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailCategory", "Event type"), s(this, e, r).call(this, `userActivity_category_${t.category}`, t.category))}
          ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailRawType", "Raw type"), t.rawType)}
          ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailSource", "Source"), I)}
          ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailTimeUtc", "Time (UTC)"), c`<time datetime=${t.occurredUtc}>${t.occurredUtc}</time>`)}
          ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailTimeLocal", "Time (local)"), L(t.occurredUtc, this.locale))}
          ${t.summary ? s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailSummary", "Summary"), t.summary) : n}
        </dl>
      </uui-box>
      <uui-box data-section="who" headline=${s(this, e, r).call(this, "userActivity_detailWho", "Who")}>
        <dl>
          ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailUser", "User"), _(t.actor, this.localize))}
          ${l ? s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailVia", "Via"), _(l, this.localize), "via") : n}
          ${t.ipAddress ? s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailIp", "IP address"), t.ipAddress, "ip") : n}
        </dl>
        ${d ? c`<p data-note="default-admin">${s(this, e, r).call(this, "userActivity_detailDefaultAdminNote")}</p>` : n}
      </uui-box>
      ${t.entity ? c`<uui-box data-section="item" headline=${s(this, e, r).call(this, "userActivity_detailItem", "Item")}>
            <dl>
              ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailItemType", "Type"), s(this, e, r).call(this, `userActivity_entityType_${t.entity.type}`, t.entity.type))}
              ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailItemName", "Name"), t.entity.name ?? "")}
              ${s(this, e, o).call(this, s(this, e, r).call(this, "userActivity_detailItemId", "Key or id"), t.entity.key ?? t.entity.id ?? "")}
            </dl>
            ${h ? c`<uui-button look="secondary" href=${h} label=${s(this, e, r).call(this, "userActivity_detailOpen", "Open")}>${s(this, e, r).call(this, "userActivity_detailOpen", "Open")}</uui-button>` : n}
          </uui-box>` : n}
      ${u.length ? c`<uui-box data-section="details" headline=${s(this, e, r).call(this, "userActivity_detailDetails", "Details")}>
            <dl>
              ${u.map(([W, g]) => {
      const b = j(g);
      return s(this, e, o).call(this, W, b !== void 0 ? c`<pre>${b}</pre>` : g);
    })}
            </dl>
          </uui-box>` : n}
    `;
  }
};
e = new WeakSet(), r = function(t, l = "") {
  return U(this.localize, t, l);
}, o = function(t, l, d) {
  return c`<div class="row" data-row=${d ?? n}><dt>${t}</dt><dd>${l}</dd></div>`;
}, p.properties = {
  event: { attribute: !1 },
  localize: { attribute: !1 },
  locale: { type: String },
  sourceKeys: { attribute: !1 }
}, p.styles = z`
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-5, 15px);
    }
    dl {
      margin: 0;
      display: grid;
      gap: var(--uui-size-space-3, 9px);
    }
    .row {
      display: grid;
      grid-template-columns: minmax(90px, 30%) 1fr;
      gap: var(--uui-size-space-4, 12px);
    }
    dt {
      font-weight: bold;
    }
    dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    pre {
      margin: 0;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
  `;
let f = p;
customElements.get("activity-detail") || customElements.define("activity-detail", f);
var H = Object.defineProperty, R = Object.getOwnPropertyDescriptor, T = (i) => {
  throw TypeError(i);
}, A = (i, a, t, l) => {
  for (var d = l > 1 ? void 0 : l ? R(a, t) : a, u = i.length - 1, h; u >= 0; u--)
    (h = i[u]) && (d = (l ? h(a, t, d) : h(d)) || d);
  return l && d && H(a, t, d), d;
}, E = (i, a, t) => a.has(i) || T("Cannot " + t), $ = (i, a, t) => (E(i, a, "read from private field"), t ? t.call(i) : a.get(i)), V = (i, a, t) => a.has(i) ? T("Cannot add the same private member more than once") : a instanceof WeakSet ? a.add(i) : a.set(i, t), B = (i, a, t) => (E(i, a, "access private method"), t), y, m, S;
let v = class extends M {
  constructor() {
    super(...arguments), V(this, y), this._failed = !1;
  }
  async firstUpdated() {
    const i = this.data;
    if (!i) return;
    const { data: a, error: t } = await N.detail(this, i.source, i.id);
    if (t || !a) {
      this._failed = !0;
      return;
    }
    this._event = a;
  }
  render() {
    return c`<umb-body-layout headline=${B(this, y, S).call(this)}>
      ${this._failed ? c`<p>${this.localize.term("userActivity_loadFailed")}</p>` : n}
      ${this._event ? c`<activity-detail
            .event=${this._event}
            .localize=${$(this, y, m)}
            .sourceKeys=${this.data?.sourceKeys ?? {}}
            .locale=${document.documentElement.lang || navigator.language}
          ></activity-detail>` : this._failed ? n : c`<uui-loader-bar></uui-loader-bar>`}
      <uui-button slot="actions" label=${this.localize.term("userActivity_close")} @click=${this._rejectModal}>${this.localize.term("userActivity_close")}</uui-button>
    </umb-body-layout>`;
  }
};
y = /* @__PURE__ */ new WeakSet();
m = function() {
  return J(this.localize);
};
S = function() {
  const i = this._event;
  if (!i) return this.localize.term("userActivity_activity");
  const a = U($(this, y, m), `userActivity_category_${i.category}`, i.category), t = i.entity?.name ?? "";
  return this.localize.term("userActivity_detailHeadline", a, t) || `${a} ${_(i.actor, $(this, y, m))}`;
};
v.styles = z`
    :host {
      display: block;
    }
  `;
A([
  O()
], v.prototype, "_event", 2);
A([
  O()
], v.prototype, "_failed", 2);
v = A([
  C("user-activity-detail-modal")
], v);
const Y = v;
export {
  v as UserActivityDetailModalElement,
  Y as default
};
