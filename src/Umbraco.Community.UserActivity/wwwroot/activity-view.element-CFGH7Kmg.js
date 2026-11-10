var ht = (e) => {
  throw TypeError(e);
};
var V = (e, i, t) => i.has(e) || ht("Cannot " + t);
var G = (e, i, t) => (V(e, i, "read from private field"), t ? t.call(e) : i.get(e)), A = (e, i, t) => i.has(e) ? ht("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(e) : i.set(e, t), pt = (e, i, t, s) => (V(e, i, "write to private field"), s ? s.call(e, t) : i.set(e, t), t), r = (e, i, t) => (V(e, i, "access private method"), t);
import { LitElement as U, html as l, nothing as f, css as S, property as lt, state as E, customElement as Pt } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as Bt } from "@umbraco-cms/backoffice/lit-element";
import { UmbModalToken as Wt, umbOpenModal as Ht } from "@umbraco-cms/backoffice/modal";
import { u as L } from "./user-activity.api-DcVZJmPO.js";
import { i as D, t as w, r as dt, b as yt, u as Vt, a as Gt, f as jt, m as Zt } from "./format-CI07-hff.js";
const gt = 30, C = { "24h": 1, "7d": 7, "30d": 30, "90d": 90 }, Yt = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, Qt = 3, vt = 100, Xt = (e) => Object.prototype.hasOwnProperty.call(C, e);
function ft(e) {
  if (!e || !/^\d{4}-\d{2}-\d{2}$/.test(e)) return;
  const i = /* @__PURE__ */ new Date(`${e}T00:00:00Z`);
  return Number.isNaN(i.getTime()) || i.toISOString().slice(0, 10) !== e ? void 0 : e;
}
const j = (e, i) => e.getAll(i).map((t) => t.trim()).filter((t) => t.length > 0 && t.length <= 64);
function Y(e) {
  const i = new URLSearchParams(e), t = i.get("user")?.trim() ?? "", s = i.get("range") ?? "", a = Number.parseInt(i.get("page") ?? "", 10);
  return {
    userKey: Yt.test(t) ? t : void 0,
    sources: j(i, "source"),
    categories: j(i, "category"),
    entityTypes: j(i, "entityType"),
    search: (i.get("search") ?? "").trim().slice(0, vt),
    range: Xt(s) ? s : void 0,
    from: ft(i.get("from")),
    to: ft(i.get("to")),
    page: Number.isInteger(a) && a > 1 ? a : 1
  };
}
function Jt(e) {
  const i = new URLSearchParams();
  e.userKey && i.set("user", e.userKey);
  for (const s of e.sources) s && i.append("source", s);
  for (const s of e.categories) s && i.append("category", s);
  for (const s of e.entityTypes) s && i.append("entityType", s);
  const t = e.search.trim();
  return t && i.set("search", t), e.range && i.set("range", e.range), e.from && i.set("from", e.from), e.to && i.set("to", e.to), e.page > 1 && i.set("page", String(e.page)), i.toString();
}
function mt(e, i = !1) {
  return !i && !!e.userKey || e.sources.length + e.categories.length + e.entityTypes.length > 0 || e.search.trim().length > 0 || !!e.range || !!e.from || !!e.to;
}
function bt(e, i) {
  return { userKey: i ? e.userKey : void 0, sources: [], categories: [], entityTypes: [], search: "", range: void 0, from: void 0, to: void 0, page: 1 };
}
function te(e, i, t, s = gt) {
  const a = { order: "desc", skip: String((Math.max(e.page, 1) - 1) * t), take: String(t) };
  e.userKey && (a.userKey = e.userKey), e.sources.length && (a.source = e.sources), e.categories.length && (a.category = e.categories), e.entityTypes.length && (a.entityType = e.entityTypes);
  const n = e.search.trim();
  if (n.length >= Qt && (a.search = n.slice(0, vt)), e.from || e.to)
    e.from && (a.from = `${e.from}T00:00:00.000Z`), e.to && (a.to = `${e.to}T23:59:59.999Z`);
  else {
    const d = e.range ? C[e.range] : s;
    a.from = new Date(i.getTime() - d * 864e5).toISOString();
  }
  return a;
}
function $t(e, i, t, s, a) {
  const n = te(e, t, s, a);
  if (!i) return { kind: "global", query: n };
  const { userKey: d, ...c } = n;
  return { kind: "user", userKey: i, query: c };
}
const ee = "UserActivity.Modal.Detail", ie = new Wt(ee, {
  modal: { type: "sidebar", size: "medium" }
}), se = 300;
var K, o, y, b, _t, wt, N, X, J, xt, At, tt, zt;
const q = class q extends U {
  constructor() {
    super();
    A(this, o);
    A(this, K);
    this.filters = { sources: [], categories: [], entityTypes: [], search: "", page: 1 }, this.options = void 0, this.localize = D, this.lockedUser = !1, this._customOpen = !1;
  }
  disconnectedCallback() {
    super.disconnectedCallback(), clearTimeout(G(this, K));
  }
  render() {
    const t = this.options, s = (t?.sources ?? []).map((c) => ({ value: c.alias, label: w(this.localize, c.displayNameKey, c.alias) })), a = (t?.categories ?? []).map((c) => ({ value: c, label: r(this, o, y).call(this, `userActivity_category_${c}`, c) })), n = (t?.entityTypes ?? []).map((c) => ({ value: c, label: r(this, o, y).call(this, `userActivity_entityType_${c}`, c) })), d = r(this, o, J).call(this) === "custom";
    return l`<div class="bar" role="search">
      <uui-input
        class="search"
        type="search"
        label=${r(this, o, y).call(this, "userActivity_filterSearch", "Search activity")}
        placeholder=${r(this, o, y).call(this, "userActivity_filterSearch", "Search activity")}
        maxlength="100"
        .value=${this.filters.search}
        @input=${r(this, o, _t)}
      ></uui-input>
      ${this.lockedUser ? f : l`<umb-user-input
            aria-label=${r(this, o, y).call(this, "userActivity_filterUser", "User")}
            max="1"
            .selection=${this.filters.userKey ? [this.filters.userKey] : []}
            @change=${(c) => r(this, o, b).call(this, { userKey: c.target.selection[0] })}
          ></umb-user-input>`}
      ${r(this, o, N).call(this, "activity-filter-source", r(this, o, y).call(this, "userActivity_filterSource", "Source"), s, this.filters.sources, (c) => r(this, o, b).call(this, { sources: c }))}
      ${r(this, o, N).call(this, "activity-filter-category", r(this, o, y).call(this, "userActivity_filterCategory", "Event type"), a, this.filters.categories, (c) => r(this, o, b).call(this, { categories: c }))}
      ${r(this, o, N).call(this, "activity-filter-entity", r(this, o, y).call(this, "userActivity_filterEntityType", "Item type"), n, this.filters.entityTypes, (c) => r(this, o, b).call(this, { entityTypes: c }))}
      ${r(this, o, At).call(this)} ${d ? l`${r(this, o, tt).call(this, "from")}${r(this, o, tt).call(this, "to")}` : f}
      ${mt(this.filters, this.lockedUser) ? l`<uui-button look="secondary" label=${r(this, o, y).call(this, "userActivity_clearFilters", "Clear filters")} @click=${() => r(this, o, zt).call(this)}
            >${r(this, o, y).call(this, "userActivity_clearFilters", "Clear filters")}</uui-button
          >` : f}
      ${t?.exportEnabled ? l`<uui-button data-action="export" look="secondary" label=${r(this, o, y).call(this, "userActivity_exportCsv", "Download CSV")} @click=${() => this.dispatchEvent(new CustomEvent("export-csv", { bubbles: !0, composed: !0 }))}
            >${r(this, o, y).call(this, "userActivity_exportCsv", "Download CSV")}</uui-button
          >` : f}
    </div>`;
  }
};
K = new WeakMap(), o = new WeakSet(), y = function(t, s = "") {
  return w(this.localize, t, s);
}, b = function(t) {
  const s = { ...this.filters, ...t, page: 1 };
  this.dispatchEvent(new CustomEvent("filters-change", { detail: s, bubbles: !0, composed: !0 }));
}, _t = function(t) {
  const s = t.target.value;
  clearTimeout(G(this, K)), pt(this, K, setTimeout(() => r(this, o, b).call(this, { search: s }), se));
}, wt = function(t, s, a) {
  return a ? [.../* @__PURE__ */ new Set([...t, s])] : t.filter((n) => n !== s);
}, N = function(t, s, a, n, d) {
  const c = n.length;
  return l`<div class="filter">
      <uui-button
        popovertarget=${t}
        look="outline"
        label=${this.localize("userActivity_filterCount", s, c)}
        aria-label=${this.localize("userActivity_filterCount", s, c)}
        >${this.localize("userActivity_filterCountBadge", s, c)}<uui-symbol-expand></uui-symbol-expand></uui-button
      >
      <uui-popover-container id=${t} placement="bottom-start">
        <div class="popover">
          ${a.map(
    (x) => l`<uui-checkbox
              label=${x.label}
              .checked=${n.includes(x.value)}
              @change=${(T) => d(r(this, o, wt).call(this, n, x.value, T.target.checked))}
              >${x.label}</uui-checkbox
            >`
  )}
        </div>
      </uui-popover-container>
    </div>`;
}, /** The preset that is the server's default window; selecting it means "no range" so the URL stays clean. */
X = function() {
  const t = this.options?.defaultRangeDays ?? gt;
  return Object.keys(C).find((s) => C[s] === t) ?? "30d";
}, J = function() {
  return this.filters.from || this.filters.to || this._customOpen ? "custom" : this.filters.range ?? r(this, o, X).call(this);
}, xt = function(t) {
  const s = t.target.value;
  if (s === "custom") {
    this._customOpen = !0;
    return;
  }
  this._customOpen = !1, r(this, o, b).call(this, { range: s === r(this, o, X).call(this) ? void 0 : s, from: void 0, to: void 0 });
}, At = function() {
  const t = r(this, o, J).call(this), a = [...Object.keys(C).map((n) => ({ name: r(this, o, y).call(this, `userActivity_range${n}`, n), value: n, selected: t === n })), { name: r(this, o, y).call(this, "userActivity_rangeCustom", "Custom"), value: "custom", selected: t === "custom" }];
  return l`<uui-select label=${r(this, o, y).call(this, "userActivity_filterDate", "Date range")} .options=${a} @change=${r(this, o, xt)}></uui-select>`;
}, tt = function(t) {
  const s = r(this, o, y).call(this, t === "from" ? "userActivity_dateFrom" : "userActivity_dateTo", t);
  return l`<uui-input
      type="date"
      label=${s}
      aria-label=${s}
      .value=${this.filters[t] ?? ""}
      @change=${(a) => r(this, o, b).call(this, { [t]: a.target.value || void 0, range: void 0 })}
    ></uui-input>`;
}, zt = function() {
  this._customOpen = !1, r(this, o, b).call(this, bt(this.filters, this.lockedUser));
}, q.properties = {
  filters: { attribute: !1 },
  options: { attribute: !1 },
  localize: { attribute: !1 },
  /** The per-user tab fixes the user, so no picker is shown. */
  lockedUser: { type: Boolean, attribute: "locked-user" },
  _customOpen: { state: !0 }
}, q.styles = S`
    .bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--uui-size-space-3, 9px);
    }
    .search {
      min-width: 220px;
    }
    .filter {
      position: relative;
    }
    .popover {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-2, 6px);
      padding: var(--uui-size-space-4, 12px);
      max-height: 320px;
      overflow-y: auto;
      background: var(--uui-color-surface, #fff);
    }
  `;
let Q = q;
customElements.get("activity-filter-bar") || customElements.define("activity-filter-bar", Q);
const M = class M extends U {
  constructor() {
    super(), this.alias = "", this.displayNameKey = "", this.localize = D;
  }
  render() {
    const i = this.displayNameKey ? this.localize(this.displayNameKey) : "", t = i && i !== this.displayNameKey ? i : this.alias;
    return l`<uui-tag look="secondary">${t}</uui-tag>`;
  }
};
M.properties = {
  alias: { type: String },
  displayNameKey: { type: String, attribute: "display-name-key" },
  localize: { attribute: !1 }
}, M.styles = S`
    :host {
      display: inline-block;
    }
  `;
let et = M;
customElements.get("activity-source-tag") || customElements.define("activity-source-tag", et);
var h, Kt, m, kt, St, Tt, Ct;
const P = class P extends U {
  constructor() {
    super();
    A(this, h);
    this.items = [], this.sourceKeys = {}, this.localize = D, this.busy = !1, this.hideUser = !1, this.locale = void 0, this.now = /* @__PURE__ */ new Date();
  }
  /** Puts focus back on a row's button, for when the detail sidebar closes (design 6.3). */
  focusRow(t, s) {
    [...this.renderRoot?.querySelectorAll("button.open") ?? []].find((n) => n.dataset.source === t && n.dataset.id === s)?.focus();
  }
  render() {
    return l`<uui-table aria-label=${r(this, h, m).call(this, "userActivity_tableCaption", "User activity, newest first")} aria-busy=${this.busy ? "true" : "false"}>
      <uui-table-head>
        <uui-table-head-cell>${r(this, h, m).call(this, "userActivity_columnWhen", "When")}</uui-table-head-cell>
        ${this.hideUser ? f : l`<uui-table-head-cell>${r(this, h, m).call(this, "userActivity_columnUser", "User")}</uui-table-head-cell>`}
        <uui-table-head-cell>${r(this, h, m).call(this, "userActivity_columnAction", "Action")}</uui-table-head-cell>
        <uui-table-head-cell>${r(this, h, m).call(this, "userActivity_columnItem", "Item")}</uui-table-head-cell>
        <uui-table-head-cell>${r(this, h, m).call(this, "userActivity_columnSource", "Source")}</uui-table-head-cell>
      </uui-table-head>
      ${this.items.map((t) => r(this, h, Ct).call(this, t))}
    </uui-table>`;
  }
};
h = new WeakSet(), Kt = function(t) {
  this.dispatchEvent(new CustomEvent("activity-open", { detail: { source: t.source, id: t.id }, bubbles: !0, composed: !0 }));
}, m = function(t, s = "") {
  return w(this.localize, t, s);
}, kt = function(t) {
  const s = dt(t.occurredUtc, this.now, this.locale), a = r(this, h, m).call(this, `userActivity_category_${t.category}`, t.category);
  return this.localize("userActivity_openDetail", s, a, t.entity?.name ?? "");
}, St = function(t) {
  const s = yt(t.actor, this.localize), a = Vt(t.actor.userKey), n = a ? l`<a href=${a}>${s}</a>` : l`<span>${s}</span>`, d = t.actor.via ? l`<small class="via">${this.localize("userActivity_via", yt(t.actor.via, this.localize))}</small>` : f;
  return l`<div class="user"><uui-avatar name=${s}></uui-avatar><div>${n}${d}</div></div>`;
}, Tt = function(t) {
  const s = t.entity;
  if (!s) return l`<span>${t.summary ?? ""}</span>`;
  const a = s.name || s.id || s.key || "", n = Gt(s), d = n ? l`<a href=${n}>${a}</a>` : l`<span>${a}</span>`;
  return l`<div class="item"><small>${r(this, h, m).call(this, `userActivity_entityType_${s.type}`, s.type)}</small>${d}</div>`;
}, Ct = function(t) {
  return l`<uui-table-row>
      <uui-table-cell>
        <button type="button" class="open" data-source=${t.source} data-id=${t.id} aria-label=${r(this, h, kt).call(this, t)} @click=${() => r(this, h, Kt).call(this, t)}>
          <time datetime=${t.occurredUtc} title=${jt(t.occurredUtc, this.locale)}>${dt(t.occurredUtc, this.now, this.locale)}</time>
        </button>
      </uui-table-cell>
      ${this.hideUser ? f : l`<uui-table-cell>${r(this, h, St).call(this, t)}</uui-table-cell>`}
      <uui-table-cell>${r(this, h, m).call(this, `userActivity_category_${t.category}`, t.category)}</uui-table-cell>
      <uui-table-cell>${r(this, h, Tt).call(this, t)}</uui-table-cell>
      <uui-table-cell>
        <activity-source-tag .alias=${t.source} .displayNameKey=${Object.hasOwn(this.sourceKeys, t.source) ? this.sourceKeys[t.source] : ""} .localize=${this.localize}></activity-source-tag>
      </uui-table-cell>
    </uui-table-row>`;
}, P.properties = {
  items: { attribute: !1 },
  sourceKeys: { attribute: !1 },
  localize: { attribute: !1 },
  busy: { type: Boolean },
  hideUser: { type: Boolean, attribute: "hide-user" },
  locale: { type: String },
  now: { attribute: !1 }
}, P.styles = S`
    :host {
      display: block;
      /* Keeps a focused row button clear of the sticky workspace header (WCAG 2.4.11). */
      scroll-padding-top: var(--uui-size-layout-3, 48px);
    }
    uui-table[aria-busy='true'] {
      opacity: 0.6;
    }
    .user,
    .item {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3, 9px);
    }
    .item {
      flex-direction: column;
      align-items: flex-start;
      gap: 0;
    }
    .via {
      display: block;
    }
    button.open {
      all: unset;
      cursor: pointer;
      display: block;
      width: 100%;
      min-height: 24px;
      text-decoration: underline;
    }
    button.open:focus-visible {
      outline: 2px solid currentColor;
      outline-offset: 2px;
    }
  `;
let it = P;
customElements.get("activity-table") || customElements.define("activity-table", it);
var k, Ut, Et;
const B = class B extends U {
  constructor() {
    super();
    A(this, k);
    this.page = 1, this.take = 50, this.count = 0, this.total = null, this.approximate = !1, this.loading = !1, this.locale = void 0, this.localize = D;
  }
  render() {
    const t = this.count === 0 ? 1 : Math.max(1, Math.ceil((this.total ?? this.page * this.take) / this.take));
    return l`<div class="pager">
      <div aria-live="polite">${r(this, k, Et).call(this)}</div>
      ${t > 1 ? l`<uui-pagination label=${w(this.localize, "userActivity_pagination", "Pagination")} .total=${t} .current=${this.page} @change=${r(this, k, Ut)}></uui-pagination>` : f}
    </div>`;
  }
};
k = new WeakSet(), Ut = function(t) {
  const s = t.target.current;
  s !== this.page && this.dispatchEvent(new CustomEvent("page-change", { detail: { page: s }, bubbles: !0, composed: !0 }));
}, /** One persistent polite region (WCAG 4.1.3): a region inserted already holding text is often not announced. */
Et = function() {
  if (this.loading) return this.localize("userActivity_loading");
  if (this.count === 0) return this.localize("userActivity_empty");
  const t = new Intl.NumberFormat(this.locale), s = (this.page - 1) * this.take + 1, a = s + this.count - 1, n = this.approximate ? "userActivity_showingRangeAtLeast" : "userActivity_showingRange";
  return this.localize(n, t.format(s), t.format(a), t.format(this.total ?? a));
}, B.properties = {
  page: { type: Number },
  take: { type: Number },
  /** Rows on the current page. */
  count: { type: Number },
  total: { attribute: !1 },
  approximate: { type: Boolean },
  loading: { type: Boolean },
  locale: { type: String },
  localize: { attribute: !1 }
}, B.styles = S`
    .pager {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--uui-size-space-3, 9px);
      margin-top: var(--uui-size-space-5, 15px);
    }
  `;
let st = B;
customElements.get("activity-pager") || customElements.define("activity-pager", st);
var g, R, Dt, Ot, Nt;
const W = class W extends U {
  constructor() {
    super();
    A(this, g);
    this.loading = !1, this.empty = !1, this.depthCapped = !1, this.status = [], this.sources = [], this.selectedSources = [], this.filterText = "", this.localize = D;
  }
  render() {
    return l`<div aria-busy=${this.loading ? "true" : "false"}>
      ${this.loading ? l`<uui-loader-bar></uui-loader-bar>` : f} ${r(this, g, Dt).call(this)}
      ${this.depthCapped ? l`<uui-box data-state="depth" role="status">${this.localize("userActivity_depthCapped")}</uui-box>` : f} ${r(this, g, Nt).call(this)}
    </div>`;
  }
};
g = new WeakSet(), R = function(t) {
  const s = this.sources.find((a) => a.alias === t);
  return s ? w(this.localize, s.displayNameKey, t) : t;
}, Dt = function() {
  const t = this.status.filter((a) => a.status !== "ok");
  if (!t.length) return f;
  const s = t.map((a) => `${r(this, g, R).call(this, a.alias)} (${w(this.localize, a.status === "timedOut" ? "userActivity_sourceTimedOut" : "userActivity_sourceFailed", a.status)})`).join(", ");
  return l`<uui-box data-state="partial" role="status"><uui-icon name="icon-alert"></uui-icon> ${this.localize("userActivity_partialWarning", s)}</uui-box>`;
}, Ot = function() {
  return this.sources.filter(
    (s) => (s.retentionHintDays || s.retentionKeptForever) && (this.selectedSources.length === 0 || this.selectedSources.includes(s.alias))
  ).map(
    (s) => s.retentionKeptForever ? l`<p class="hint">${this.localize("userActivity_retentionKeptForever", r(this, g, R).call(this, s.alias))}</p>` : l`<p class="hint">${this.localize("userActivity_retentionHint", r(this, g, R).call(this, s.alias), s.retentionHintDays)}</p>`
  );
}, Nt = function() {
  if (!this.empty || this.loading) return f;
  const t = this.filterText ? this.localize("userActivity_emptyFilters", this.filterText) : this.localize("userActivity_emptyFiltersNone");
  return l`<div data-state="empty" class="empty">
      <p><strong>${w(this.localize, "userActivity_empty", "No activity found.")}</strong></p>
      <p>${t}</p>
      ${r(this, g, Ot).call(this)}
      <slot name="actions"></slot>
    </div>`;
}, W.properties = {
  loading: { type: Boolean },
  empty: { type: Boolean },
  depthCapped: { type: Boolean, attribute: "depth-capped" },
  status: { attribute: !1 },
  sources: { attribute: !1 },
  selectedSources: { attribute: !1 },
  /** The active filters in words, for the empty state. */
  filterText: { type: String, attribute: "filter-text" },
  localize: { attribute: !1 }
}, W.styles = S`
    uui-box {
      margin-top: var(--uui-size-space-4, 12px);
    }
    .empty {
      text-align: center;
      padding: var(--uui-size-layout-2, 36px) 0;
    }
    .hint {
      margin: var(--uui-size-space-2, 6px) 0;
    }
  `;
let at = W;
customElements.get("activity-states") || customElements.define("activity-states", at);
var ae = Object.defineProperty, re = Object.getOwnPropertyDescriptor, Rt = (e) => {
  throw TypeError(e);
}, _ = (e, i, t, s) => {
  for (var a = s > 1 ? void 0 : s ? re(i, t) : i, n = e.length - 1, d; n >= 0; n--)
    (d = e[n]) && (a = (s ? d(i, t, a) : d(a)) || a);
  return s && a && ae(i, t, a), a;
}, nt = (e, i, t) => i.has(e) || Rt("Cannot " + t), $ = (e, i, t) => (nt(e, i, "read from private field"), t ? t.call(e) : i.get(e)), Z = (e, i, t) => i.has(e) ? Rt("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(e) : i.set(e, t), oe = (e, i, t, s) => (nt(e, i, "write to private field"), i.set(e, t), t), p = (e, i, t) => (nt(e, i, "access private method"), t), le = (e, i, t, s) => ({
  set _(a) {
    oe(e, i, a);
  },
  get _() {
    return $(e, i, s);
  }
}), F, I, u, z, rt, It, H, O, ot, Lt, Ft, qt, Mt, ct;
const ut = 50;
let v = class extends Bt {
  constructor() {
    super(...arguments), Z(this, u), this.noUrl = !1, this._filters = Y(""), this._loading = !0, this._forbidden = !1, Z(this, F, 0), Z(this, I, () => p(this, u, rt).call(this));
  }
  connectedCallback() {
    super.connectedCallback(), this.noUrl || window.addEventListener("popstate", $(this, I)), p(this, u, It).call(this).then(() => p(this, u, rt).call(this));
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.removeEventListener("popstate", $(this, I));
  }
  updated(e) {
    e.has("userKey") && e.get("userKey") !== void 0 && (this._filters = { ...this._filters, page: 1 }, p(this, u, H).call(this, this._filters, !1), p(this, u, O).call(this));
  }
  render() {
    if (this._forbidden) return l`<uui-box><p>${this.localize.term("userActivity_noAccess")}</p></uui-box>`;
    const e = this._page, i = document.documentElement.lang || navigator.language;
    return l`
      ${this.heading ? l`<h2>${this.heading}</h2>` : f}
      <activity-filter-bar
        .filters=${this._filters}
        .options=${this._options}
        .localize=${$(this, u, z)}
        ?locked-user=${!!this.userKey}
        @filters-change=${p(this, u, ot)}
        @export-csv=${p(this, u, qt)}
      ></activity-filter-bar>
      <activity-states
        ?loading=${this._loading}
        ?empty=${!this._loading && !!e && e.items.length === 0}
        ?depth-capped=${e?.depthCapped ?? !1}
        .status=${e?.sources ?? []}
        .sources=${this._options?.sources ?? []}
        .selectedSources=${this._filters.sources}
        filter-text=${p(this, u, Mt).call(this)}
        .localize=${$(this, u, z)}
      >
        ${mt(this._filters, !!this.userKey) ? l`<uui-button slot="actions" look="secondary" label=${this.localize.term("userActivity_clearFilters")} @click=${() => p(this, u, ot).call(this, new CustomEvent("filters-change", { detail: bt(this._filters, !!this.userKey) }))}
              >${this.localize.term("userActivity_clearFilters")}</uui-button
            >` : f}
      </activity-states>
      <activity-table
        .items=${e?.items ?? []}
        .sourceKeys=${p(this, u, ct).call(this)}
        .localize=${$(this, u, z)}
        .locale=${i}
        .now=${/* @__PURE__ */ new Date()}
        ?busy=${this._loading}
        ?hide-user=${!!this.userKey}
        @activity-open=${p(this, u, Ft)}
      ></activity-table>
      <activity-pager
        .page=${this._filters.page}
        .take=${ut}
        .count=${e?.items.length ?? 0}
        .total=${e?.total ?? null}
        ?approximate=${e ? !e.totalIsExact : !1}
        .locale=${i}
        .localize=${$(this, u, z)}
        @page-change=${p(this, u, Lt)}
      ></activity-pager>
    `;
  }
};
F = /* @__PURE__ */ new WeakMap();
I = /* @__PURE__ */ new WeakMap();
u = /* @__PURE__ */ new WeakSet();
z = function() {
  return Zt(this.localize);
};
rt = function() {
  const e = this.noUrl ? "" : window.location.search;
  this._filters = { ...Y(e), userKey: this.userKey ?? Y(e).userKey }, p(this, u, O).call(this);
};
It = async function() {
  const { data: e, error: i } = await L.filterOptions(this);
  if (i) {
    this._forbidden = i.status === 403, this._loading = !1;
    return;
  }
  this._options = e;
};
H = function(e, i) {
  if (this.noUrl) return;
  const t = this.userKey ? { ...e, userKey: void 0 } : e, s = Jt(t), a = `${window.location.pathname}${s ? `?${s}` : ""}${window.location.hash}`;
  a !== `${window.location.pathname}${window.location.search}${window.location.hash}` && (i ? window.history.pushState(null, "", a) : window.history.replaceState(null, "", a));
};
O = async function() {
  if (this._forbidden) return;
  const e = ++le(this, F)._;
  this._loading = !0;
  const i = $t(this._filters, this.userKey, /* @__PURE__ */ new Date(), ut, this._options?.defaultRangeDays), t = i.kind === "user" ? await L.listForUser(this, i.userKey, i.query) : await L.list(this, i.query);
  if (e === $(this, F)) {
    if (this._loading = !1, t.error) {
      this._forbidden = t.error.status === 403;
      return;
    }
    this._forbidden = !1, this._page = t.data;
  }
};
ot = function(e) {
  const i = e.detail;
  this._filters = { ...i, userKey: this.userKey ?? i.userKey }, p(this, u, H).call(this, this._filters, !1), p(this, u, O).call(this);
};
Lt = function(e) {
  const { page: i } = e.detail;
  this._filters = { ...this._filters, page: i }, p(this, u, H).call(this, this._filters, !0), p(this, u, O).call(this);
};
Ft = async function(e) {
  const { source: i, id: t } = e.detail;
  try {
    await Ht(this, ie, { data: { source: i, id: t, sourceKeys: p(this, u, ct).call(this) } });
  } catch {
  }
  this.renderRoot.querySelector("activity-table")?.focusRow(i, t);
};
qt = async function() {
  const e = $t(this._filters, this.userKey, /* @__PURE__ */ new Date(), ut, this._options?.defaultRangeDays), { skip: i, take: t, order: s, ...a } = e.query, n = e.kind === "user" ? { ...a, userKey: e.userKey } : a, { data: d, error: c } = await L.exportCsv(this, n);
  if (c || !d) return;
  const x = URL.createObjectURL(d), T = document.createElement("a");
  T.href = x, T.download = `user-activity-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 16).replace(/[-:T]/g, "").replace(/^(\d{8})/, "$1-")}.csv`, T.click(), setTimeout(() => URL.revokeObjectURL(x), 1e4);
};
Mt = function() {
  const e = this._filters, i = (s, a) => w($(this, u, z), s, a), t = (this._options?.sources ?? []).filter((s) => e.sources.includes(s.alias)).map((s) => i(s.displayNameKey, s.alias));
  return [
    e.search.trim() ? `“${e.search.trim()}”` : "",
    ...t,
    ...e.categories.map((s) => i(`userActivity_category_${s}`, s)),
    ...e.entityTypes.map((s) => i(`userActivity_entityType_${s}`, s))
  ].filter(Boolean).join(", ");
};
ct = function() {
  return Object.fromEntries((this._options?.sources ?? []).map((e) => [e.alias, e.displayNameKey]));
};
v.styles = S`
    :host {
      display: block;
      padding: var(--uui-size-layout-1, 24px);
    }
    activity-table,
    activity-states {
      margin-top: var(--uui-size-space-5, 15px);
    }
  `;
_([
  lt({ type: String, attribute: "user-key" })
], v.prototype, "userKey", 2);
_([
  lt({ type: String })
], v.prototype, "heading", 2);
_([
  lt({ type: Boolean, attribute: "no-url" })
], v.prototype, "noUrl", 2);
_([
  E()
], v.prototype, "_filters", 2);
_([
  E()
], v.prototype, "_options", 2);
_([
  E()
], v.prototype, "_page", 2);
_([
  E()
], v.prototype, "_loading", 2);
_([
  E()
], v.prototype, "_forbidden", 2);
v = _([
  Pt("user-activity-view")
], v);
