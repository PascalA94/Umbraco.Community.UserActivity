var ut = (e) => {
  throw TypeError(e);
};
var H = (e, i, t) => i.has(e) || ut("Cannot " + t);
var V = (e, i, t) => (H(e, i, "read from private field"), t ? t.call(e) : i.get(e)), A = (e, i, t) => i.has(e) ? ut("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(e) : i.set(e, t), ht = (e, i, t, s) => (H(e, i, "write to private field"), s ? s.call(e, t) : i.set(e, t), t), r = (e, i, t) => (H(e, i, "access private method"), t);
import { LitElement as U, html as n, nothing as f, css as S, property as ft, state as E, customElement as Pt } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as Wt } from "@umbraco-cms/backoffice/lit-element";
import { UmbModalToken as Bt, umbOpenModal as Ht } from "@umbraco-cms/backoffice/modal";
import { u as I } from "./user-activity.api-DcVZJmPO.js";
import { i as D, t as _, r as pt, b as dt, u as Vt, a as Gt, f as jt, m as Zt } from "./format-CI07-hff.js";
const gt = 30, C = { "24h": 1, "7d": 7, "30d": 30, "90d": 90 }, Yt = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, Qt = 3, vt = 100, Xt = (e) => Object.prototype.hasOwnProperty.call(C, e);
function yt(e) {
  if (!e || !/^\d{4}-\d{2}-\d{2}$/.test(e)) return;
  const i = /* @__PURE__ */ new Date(`${e}T00:00:00Z`);
  return Number.isNaN(i.getTime()) || i.toISOString().slice(0, 10) !== e ? void 0 : e;
}
const G = (e, i) => e.getAll(i).map((t) => t.trim()).filter((t) => t.length > 0 && t.length <= 64);
function Z(e) {
  const i = new URLSearchParams(e), t = i.get("user")?.trim() ?? "", s = i.get("range") ?? "", a = Number.parseInt(i.get("page") ?? "", 10);
  return {
    userKey: Yt.test(t) ? t : void 0,
    sources: G(i, "source"),
    categories: G(i, "category"),
    entityTypes: G(i, "entityType"),
    search: (i.get("search") ?? "").trim().slice(0, vt),
    range: Xt(s) ? s : void 0,
    from: yt(i.get("from")),
    to: yt(i.get("to")),
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
  const l = e.search.trim();
  if (l.length >= Qt && (a.search = l.slice(0, vt)), e.from || e.to)
    e.from && (a.from = `${e.from}T00:00:00.000Z`), e.to && (a.to = `${e.to}T23:59:59.999Z`);
  else {
    const d = e.range ? C[e.range] : s;
    a.from = new Date(i.getTime() - d * 864e5).toISOString();
  }
  return a;
}
function $t(e, i, t, s, a) {
  const l = te(e, t, s, a);
  if (!i) return { kind: "global", query: l };
  const { userKey: d, ...c } = l;
  return { kind: "user", userKey: i, query: c };
}
const ee = "UserActivity.Modal.Detail", ie = new Bt(ee, {
  modal: { type: "sidebar", size: "medium" }
}), se = 300;
var k, o, y, b, _t, wt, N, Q, X, xt, At, J, zt;
const q = class q extends U {
  constructor() {
    super();
    A(this, o);
    A(this, k);
    this.filters = { sources: [], categories: [], entityTypes: [], search: "", page: 1 }, this.options = void 0, this.localize = D, this.lockedUser = !1, this._customOpen = !1;
  }
  disconnectedCallback() {
    super.disconnectedCallback(), clearTimeout(V(this, k));
  }
  render() {
    const t = this.options, s = (t?.sources ?? []).map((c) => ({ value: c.alias, label: _(this.localize, c.displayNameKey, c.alias) })), a = (t?.categories ?? []).map((c) => ({ value: c, label: r(this, o, y).call(this, `userActivity_category_${c}`, c) })), l = (t?.entityTypes ?? []).map((c) => ({ value: c, label: r(this, o, y).call(this, `userActivity_entityType_${c}`, c) })), d = r(this, o, X).call(this) === "custom";
    return n`<div class="bar" role="search">
      <uui-input
        class="search"
        type="search"
        label=${r(this, o, y).call(this, "userActivity_filterSearch", "Search activity")}
        placeholder=${r(this, o, y).call(this, "userActivity_filterSearch", "Search activity")}
        maxlength="100"
        .value=${this.filters.search}
        @input=${r(this, o, _t)}
      ></uui-input>
      ${this.lockedUser ? f : n`<umb-user-input
            aria-label=${r(this, o, y).call(this, "userActivity_filterUser", "User")}
            max="1"
            .selection=${this.filters.userKey ? [this.filters.userKey] : []}
            @change=${(c) => r(this, o, b).call(this, { userKey: c.target.selection[0] })}
          ></umb-user-input>`}
      ${r(this, o, N).call(this, "activity-filter-source", r(this, o, y).call(this, "userActivity_filterSource", "Source"), s, this.filters.sources, (c) => r(this, o, b).call(this, { sources: c }))}
      ${r(this, o, N).call(this, "activity-filter-category", r(this, o, y).call(this, "userActivity_filterCategory", "Event type"), a, this.filters.categories, (c) => r(this, o, b).call(this, { categories: c }))}
      ${r(this, o, N).call(this, "activity-filter-entity", r(this, o, y).call(this, "userActivity_filterEntityType", "Item type"), l, this.filters.entityTypes, (c) => r(this, o, b).call(this, { entityTypes: c }))}
      ${r(this, o, At).call(this)} ${d ? n`${r(this, o, J).call(this, "from")}${r(this, o, J).call(this, "to")}` : f}
      ${mt(this.filters, this.lockedUser) ? n`<uui-button look="secondary" label=${r(this, o, y).call(this, "userActivity_clearFilters", "Clear filters")} @click=${() => r(this, o, zt).call(this)}
            >${r(this, o, y).call(this, "userActivity_clearFilters", "Clear filters")}</uui-button
          >` : f}
      ${t?.exportEnabled ? n`<uui-button data-action="export" look="secondary" label=${r(this, o, y).call(this, "userActivity_exportCsv", "Download CSV")} @click=${() => this.dispatchEvent(new CustomEvent("export-csv", { bubbles: !0, composed: !0 }))}
            >${r(this, o, y).call(this, "userActivity_exportCsv", "Download CSV")}</uui-button
          >` : f}
    </div>`;
  }
};
k = new WeakMap(), o = new WeakSet(), y = function(t, s = "") {
  return _(this.localize, t, s);
}, b = function(t) {
  const s = { ...this.filters, ...t, page: 1 };
  this.dispatchEvent(new CustomEvent("filters-change", { detail: s, bubbles: !0, composed: !0 }));
}, _t = function(t) {
  const s = t.target.value;
  clearTimeout(V(this, k)), ht(this, k, setTimeout(() => r(this, o, b).call(this, { search: s }), se));
}, wt = function(t, s, a) {
  return a ? [.../* @__PURE__ */ new Set([...t, s])] : t.filter((l) => l !== s);
}, N = function(t, s, a, l, d) {
  const c = l.length;
  return n`<div class="filter">
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
    (x) => n`<uui-checkbox
              label=${x.label}
              .checked=${l.includes(x.value)}
              @change=${(T) => d(r(this, o, wt).call(this, l, x.value, T.target.checked))}
              >${x.label}</uui-checkbox
            >`
  )}
        </div>
      </uui-popover-container>
    </div>`;
}, /** The preset that is the server's default window; selecting it means "no range" so the URL stays clean. */
Q = function() {
  const t = this.options?.defaultRangeDays ?? gt;
  return Object.keys(C).find((s) => C[s] === t) ?? "30d";
}, X = function() {
  return this.filters.from || this.filters.to || this._customOpen ? "custom" : this.filters.range ?? r(this, o, Q).call(this);
}, xt = function(t) {
  const s = t.target.value;
  if (s === "custom") {
    this._customOpen = !0;
    return;
  }
  this._customOpen = !1, r(this, o, b).call(this, { range: s === r(this, o, Q).call(this) ? void 0 : s, from: void 0, to: void 0 });
}, At = function() {
  const t = r(this, o, X).call(this), a = [...Object.keys(C).map((l) => ({ name: r(this, o, y).call(this, `userActivity_range${l}`, l), value: l, selected: t === l })), { name: r(this, o, y).call(this, "userActivity_rangeCustom", "Custom"), value: "custom", selected: t === "custom" }];
  return n`<uui-select label=${r(this, o, y).call(this, "userActivity_filterDate", "Date range")} .options=${a} @change=${r(this, o, xt)}></uui-select>`;
}, J = function(t) {
  const s = r(this, o, y).call(this, t === "from" ? "userActivity_dateFrom" : "userActivity_dateTo", t);
  return n`<uui-input
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
let Y = q;
customElements.get("activity-filter-bar") || customElements.define("activity-filter-bar", Y);
const M = class M extends U {
  constructor() {
    super(), this.alias = "", this.displayNameKey = "", this.localize = D;
  }
  render() {
    const i = this.displayNameKey ? this.localize(this.displayNameKey) : "", t = i && i !== this.displayNameKey ? i : this.alias;
    return n`<uui-tag look="secondary">${t}</uui-tag>`;
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
let tt = M;
customElements.get("activity-source-tag") || customElements.define("activity-source-tag", tt);
var h, kt, v, Kt, St, Tt, Ct;
const F = class F extends U {
  constructor() {
    super();
    A(this, h);
    this.items = [], this.sourceKeys = {}, this.localize = D, this.busy = !1, this.hideUser = !1, this.locale = void 0, this.now = /* @__PURE__ */ new Date();
  }
  /** Puts focus back on a row's button, for when the detail sidebar closes (design 6.3). */
  focusRow(t, s) {
    [...this.renderRoot?.querySelectorAll("button.open") ?? []].find((l) => l.dataset.source === t && l.dataset.id === s)?.focus();
  }
  render() {
    return n`<uui-table aria-label=${r(this, h, v).call(this, "userActivity_tableCaption", "User activity, newest first")} aria-busy=${this.busy ? "true" : "false"}>
      <uui-table-head>
        <uui-table-head-cell>${r(this, h, v).call(this, "userActivity_columnWhen", "When")}</uui-table-head-cell>
        ${this.hideUser ? f : n`<uui-table-head-cell>${r(this, h, v).call(this, "userActivity_columnUser", "User")}</uui-table-head-cell>`}
        <uui-table-head-cell>${r(this, h, v).call(this, "userActivity_columnAction", "Action")}</uui-table-head-cell>
        <uui-table-head-cell>${r(this, h, v).call(this, "userActivity_columnItem", "Item")}</uui-table-head-cell>
        <uui-table-head-cell>${r(this, h, v).call(this, "userActivity_columnSource", "Source")}</uui-table-head-cell>
      </uui-table-head>
      ${this.items.map((t) => r(this, h, Ct).call(this, t))}
    </uui-table>`;
  }
};
h = new WeakSet(), kt = function(t) {
  this.dispatchEvent(new CustomEvent("activity-open", { detail: { source: t.source, id: t.id }, bubbles: !0, composed: !0 }));
}, v = function(t, s = "") {
  return _(this.localize, t, s);
}, Kt = function(t) {
  const s = pt(t.occurredUtc, this.now, this.locale), a = r(this, h, v).call(this, `userActivity_category_${t.category}`, t.category);
  return this.localize("userActivity_openDetail", s, a, t.entity?.name ?? "");
}, St = function(t) {
  const s = dt(t.actor, this.localize), a = Vt(t.actor.userKey), l = a ? n`<a href=${a}>${s}</a>` : n`<span>${s}</span>`, d = t.actor.via ? n`<small class="via">${this.localize("userActivity_via", dt(t.actor.via, this.localize))}</small>` : f;
  return n`<div class="user"><uui-avatar name=${s}></uui-avatar><div>${l}${d}</div></div>`;
}, Tt = function(t) {
  const s = t.entity;
  if (!s) return n`<span>${t.summary ?? ""}</span>`;
  const a = s.name || s.id || s.key || "", l = Gt(s), d = l ? n`<a href=${l}>${a}</a>` : n`<span>${a}</span>`;
  return n`<div class="item"><small>${r(this, h, v).call(this, `userActivity_entityType_${s.type}`, s.type)}</small>${d}</div>`;
}, Ct = function(t) {
  return n`<uui-table-row>
      <uui-table-cell>
        <button type="button" class="open" data-source=${t.source} data-id=${t.id} aria-label=${r(this, h, Kt).call(this, t)} @click=${() => r(this, h, kt).call(this, t)}>
          <time datetime=${t.occurredUtc} title=${jt(t.occurredUtc, this.locale)}>${pt(t.occurredUtc, this.now, this.locale)}</time>
        </button>
      </uui-table-cell>
      ${this.hideUser ? f : n`<uui-table-cell>${r(this, h, St).call(this, t)}</uui-table-cell>`}
      <uui-table-cell>${r(this, h, v).call(this, `userActivity_category_${t.category}`, t.category)}</uui-table-cell>
      <uui-table-cell>${r(this, h, Tt).call(this, t)}</uui-table-cell>
      <uui-table-cell>
        <activity-source-tag .alias=${t.source} .displayNameKey=${Object.hasOwn(this.sourceKeys, t.source) ? this.sourceKeys[t.source] : ""} .localize=${this.localize}></activity-source-tag>
      </uui-table-cell>
    </uui-table-row>`;
}, F.properties = {
  items: { attribute: !1 },
  sourceKeys: { attribute: !1 },
  localize: { attribute: !1 },
  busy: { type: Boolean },
  hideUser: { type: Boolean, attribute: "hide-user" },
  locale: { type: String },
  now: { attribute: !1 }
}, F.styles = S`
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
let et = F;
customElements.get("activity-table") || customElements.define("activity-table", et);
var K, Ut, Et;
const P = class P extends U {
  constructor() {
    super();
    A(this, K);
    this.page = 1, this.take = 50, this.count = 0, this.total = null, this.approximate = !1, this.loading = !1, this.locale = void 0, this.localize = D;
  }
  render() {
    const t = this.count === 0 ? 1 : Math.max(1, Math.ceil((this.total ?? this.page * this.take) / this.take));
    return n`<div class="pager">
      <div aria-live="polite">${r(this, K, Et).call(this)}</div>
      ${t > 1 ? n`<uui-pagination label=${_(this.localize, "userActivity_pagination", "Pagination")} .total=${t} .current=${this.page} @change=${r(this, K, Ut)}></uui-pagination>` : f}
    </div>`;
  }
};
K = new WeakSet(), Ut = function(t) {
  const s = t.target.current;
  s !== this.page && this.dispatchEvent(new CustomEvent("page-change", { detail: { page: s }, bubbles: !0, composed: !0 }));
}, /** One persistent polite region (WCAG 4.1.3): a region inserted already holding text is often not announced. */
Et = function() {
  if (this.loading) return this.localize("userActivity_loading");
  if (this.count === 0) return this.localize("userActivity_empty");
  const t = new Intl.NumberFormat(this.locale), s = (this.page - 1) * this.take + 1, a = s + this.count - 1, l = this.approximate ? "userActivity_showingRangeAtLeast" : "userActivity_showingRange";
  return this.localize(l, t.format(s), t.format(a), t.format(this.total ?? a));
}, P.properties = {
  page: { type: Number },
  take: { type: Number },
  /** Rows on the current page. */
  count: { type: Number },
  total: { attribute: !1 },
  approximate: { type: Boolean },
  loading: { type: Boolean },
  locale: { type: String },
  localize: { attribute: !1 }
}, P.styles = S`
    .pager {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--uui-size-space-3, 9px);
      margin-top: var(--uui-size-space-5, 15px);
    }
  `;
let it = P;
customElements.get("activity-pager") || customElements.define("activity-pager", it);
var g, at, Dt, Ot, Nt;
const W = class W extends U {
  constructor() {
    super();
    A(this, g);
    this.loading = !1, this.empty = !1, this.depthCapped = !1, this.status = [], this.sources = [], this.selectedSources = [], this.filterText = "", this.localize = D;
  }
  render() {
    return n`<div aria-busy=${this.loading ? "true" : "false"}>
      ${this.loading ? n`<uui-loader-bar></uui-loader-bar>` : f} ${r(this, g, Dt).call(this)}
      ${this.depthCapped ? n`<uui-box data-state="depth" role="status">${this.localize("userActivity_depthCapped")}</uui-box>` : f} ${r(this, g, Nt).call(this)}
    </div>`;
  }
};
g = new WeakSet(), at = function(t) {
  const s = this.sources.find((a) => a.alias === t);
  return s ? _(this.localize, s.displayNameKey, t) : t;
}, Dt = function() {
  const t = this.status.filter((a) => a.status !== "ok");
  if (!t.length) return f;
  const s = t.map((a) => `${r(this, g, at).call(this, a.alias)} (${_(this.localize, a.status === "timedOut" ? "userActivity_sourceTimedOut" : "userActivity_sourceFailed", a.status)})`).join(", ");
  return n`<uui-box data-state="partial" role="status"><uui-icon name="icon-alert"></uui-icon> ${this.localize("userActivity_partialWarning", s)}</uui-box>`;
}, Ot = function() {
  return this.sources.filter((s) => s.retentionHintDays && (this.selectedSources.length === 0 || this.selectedSources.includes(s.alias))).map((s) => n`<p class="hint">${this.localize("userActivity_retentionHint", r(this, g, at).call(this, s.alias), s.retentionHintDays)}</p>`);
}, Nt = function() {
  if (!this.empty || this.loading) return f;
  const t = this.filterText ? this.localize("userActivity_emptyFilters", this.filterText) : this.localize("userActivity_emptyFiltersNone");
  return n`<div data-state="empty" class="empty">
      <p><strong>${_(this.localize, "userActivity_empty", "No activity found.")}</strong></p>
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
let st = W;
customElements.get("activity-states") || customElements.define("activity-states", st);
var ae = Object.defineProperty, re = Object.getOwnPropertyDescriptor, Rt = (e) => {
  throw TypeError(e);
}, w = (e, i, t, s) => {
  for (var a = s > 1 ? void 0 : s ? re(i, t) : i, l = e.length - 1, d; l >= 0; l--)
    (d = e[l]) && (a = (s ? d(i, t, a) : d(a)) || a);
  return s && a && ae(i, t, a), a;
}, lt = (e, i, t) => i.has(e) || Rt("Cannot " + t), $ = (e, i, t) => (lt(e, i, "read from private field"), t ? t.call(e) : i.get(e)), j = (e, i, t) => i.has(e) ? Rt("Cannot add the same private member more than once") : i instanceof WeakSet ? i.add(e) : i.set(e, t), oe = (e, i, t, s) => (lt(e, i, "write to private field"), i.set(e, t), t), p = (e, i, t) => (lt(e, i, "access private method"), t), le = (e, i, t, s) => ({
  set _(a) {
    oe(e, i, a);
  },
  get _() {
    return $(e, i, s);
  }
}), L, R, u, z, rt, It, B, O, ot, Lt, qt, Mt, Ft, nt;
const ct = 50;
let m = class extends Wt {
  constructor() {
    super(...arguments), j(this, u), this._filters = Z(""), this._loading = !0, this._forbidden = !1, j(this, L, 0), j(this, R, () => p(this, u, rt).call(this));
  }
  connectedCallback() {
    super.connectedCallback(), window.addEventListener("popstate", $(this, R)), p(this, u, It).call(this).then(() => p(this, u, rt).call(this));
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.removeEventListener("popstate", $(this, R));
  }
  updated(e) {
    e.has("userKey") && e.get("userKey") !== void 0 && (this._filters = { ...this._filters, page: 1 }, p(this, u, B).call(this, this._filters, !1), p(this, u, O).call(this));
  }
  render() {
    if (this._forbidden) return n`<uui-box><p>${this.localize.term("userActivity_noAccess")}</p></uui-box>`;
    const e = this._page, i = document.documentElement.lang || navigator.language;
    return n`
      ${this.heading ? n`<h2>${this.heading}</h2>` : f}
      <activity-filter-bar
        .filters=${this._filters}
        .options=${this._options}
        .localize=${$(this, u, z)}
        ?locked-user=${!!this.userKey}
        @filters-change=${p(this, u, ot)}
        @export-csv=${p(this, u, Mt)}
      ></activity-filter-bar>
      <activity-states
        ?loading=${this._loading}
        ?empty=${!this._loading && !!e && e.items.length === 0}
        ?depth-capped=${e?.depthCapped ?? !1}
        .status=${e?.sources ?? []}
        .sources=${this._options?.sources ?? []}
        .selectedSources=${this._filters.sources}
        filter-text=${p(this, u, Ft).call(this)}
        .localize=${$(this, u, z)}
      >
        ${mt(this._filters, !!this.userKey) ? n`<uui-button slot="actions" look="secondary" label=${this.localize.term("userActivity_clearFilters")} @click=${() => p(this, u, ot).call(this, new CustomEvent("filters-change", { detail: bt(this._filters, !!this.userKey) }))}
              >${this.localize.term("userActivity_clearFilters")}</uui-button
            >` : f}
      </activity-states>
      <activity-table
        .items=${e?.items ?? []}
        .sourceKeys=${p(this, u, nt).call(this)}
        .localize=${$(this, u, z)}
        .locale=${i}
        .now=${/* @__PURE__ */ new Date()}
        ?busy=${this._loading}
        ?hide-user=${!!this.userKey}
        @activity-open=${p(this, u, qt)}
      ></activity-table>
      <activity-pager
        .page=${this._filters.page}
        .take=${ct}
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
L = /* @__PURE__ */ new WeakMap();
R = /* @__PURE__ */ new WeakMap();
u = /* @__PURE__ */ new WeakSet();
z = function() {
  return Zt(this.localize);
};
rt = function() {
  this._filters = { ...Z(window.location.search), userKey: this.userKey ?? Z(window.location.search).userKey }, p(this, u, O).call(this);
};
It = async function() {
  const { data: e, error: i } = await I.filterOptions(this);
  if (i) {
    this._forbidden = i.status === 403, this._loading = !1;
    return;
  }
  this._options = e;
};
B = function(e, i) {
  const t = this.userKey ? { ...e, userKey: void 0 } : e, s = Jt(t), a = `${window.location.pathname}${s ? `?${s}` : ""}${window.location.hash}`;
  a !== `${window.location.pathname}${window.location.search}${window.location.hash}` && (i ? window.history.pushState(null, "", a) : window.history.replaceState(null, "", a));
};
O = async function() {
  if (this._forbidden) return;
  const e = ++le(this, L)._;
  this._loading = !0;
  const i = $t(this._filters, this.userKey, /* @__PURE__ */ new Date(), ct, this._options?.defaultRangeDays), t = i.kind === "user" ? await I.listForUser(this, i.userKey, i.query) : await I.list(this, i.query);
  if (e === $(this, L)) {
    if (this._loading = !1, t.error) {
      this._forbidden = t.error.status === 403;
      return;
    }
    this._forbidden = !1, this._page = t.data;
  }
};
ot = function(e) {
  const i = e.detail;
  this._filters = { ...i, userKey: this.userKey ?? i.userKey }, p(this, u, B).call(this, this._filters, !1), p(this, u, O).call(this);
};
Lt = function(e) {
  const { page: i } = e.detail;
  this._filters = { ...this._filters, page: i }, p(this, u, B).call(this, this._filters, !0), p(this, u, O).call(this);
};
qt = async function(e) {
  const { source: i, id: t } = e.detail;
  try {
    await Ht(this, ie, { data: { source: i, id: t, sourceKeys: p(this, u, nt).call(this) } });
  } catch {
  }
  this.renderRoot.querySelector("activity-table")?.focusRow(i, t);
};
Mt = async function() {
  const e = $t(this._filters, this.userKey, /* @__PURE__ */ new Date(), ct, this._options?.defaultRangeDays), { skip: i, take: t, order: s, ...a } = e.query, l = e.kind === "user" ? { ...a, userKey: e.userKey } : a, { data: d, error: c } = await I.exportCsv(this, l);
  if (c || !d) return;
  const x = URL.createObjectURL(d), T = document.createElement("a");
  T.href = x, T.download = `user-activity-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 16).replace(/[-:T]/g, "").replace(/^(\d{8})/, "$1-")}.csv`, T.click(), setTimeout(() => URL.revokeObjectURL(x), 1e4);
};
Ft = function() {
  const e = this._filters, i = (s, a) => _($(this, u, z), s, a), t = (this._options?.sources ?? []).filter((s) => e.sources.includes(s.alias)).map((s) => i(s.displayNameKey, s.alias));
  return [
    e.search.trim() ? `“${e.search.trim()}”` : "",
    ...t,
    ...e.categories.map((s) => i(`userActivity_category_${s}`, s)),
    ...e.entityTypes.map((s) => i(`userActivity_entityType_${s}`, s))
  ].filter(Boolean).join(", ");
};
nt = function() {
  return Object.fromEntries((this._options?.sources ?? []).map((e) => [e.alias, e.displayNameKey]));
};
m.styles = S`
    :host {
      display: block;
      padding: var(--uui-size-layout-1, 24px);
    }
    activity-table,
    activity-states {
      margin-top: var(--uui-size-space-5, 15px);
    }
  `;
w([
  ft({ type: String, attribute: "user-key" })
], m.prototype, "userKey", 2);
w([
  ft({ type: String })
], m.prototype, "heading", 2);
w([
  E()
], m.prototype, "_filters", 2);
w([
  E()
], m.prototype, "_options", 2);
w([
  E()
], m.prototype, "_page", 2);
w([
  E()
], m.prototype, "_loading", 2);
w([
  E()
], m.prototype, "_forbidden", 2);
m = w([
  Pt("user-activity-view")
], m);
