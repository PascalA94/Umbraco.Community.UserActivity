import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { userActivityApi } from '../../api/user-activity.api.js';
import type { ActivityFilterOptionsResponseModel, ActivityPageResponseModel } from '../../api/user-activity.api.js';
import { clearFilters, filtersFromSearch, filtersToSearch, type ActivityFilters } from '../../state/activity-filter-state.js';
import { buildRequest } from '../../state/activity-request.js';
import { makeLocalize, type Localize } from '../../components/localize.js';
import { hasActiveFilters } from '../../state/activity-filter-state.js';
import { ACTIVITY_DETAIL_MODAL } from '../../modal/activity-detail.modal-token.js';
import { term } from '../../components/format.js';
import type { ActivityTableElement } from '../../components/activity-table.element.js';
import '../../components/activity-filter-bar.element.js';
import '../../components/activity-table.element.js';
import '../../components/activity-pager.element.js';
import '../../components/activity-states.element.js';

const PAGE_SIZE = 50;

/**
 * The view both entry points share (design 6.1, 6.2): filter bar, table and pager over one query. With `userKey`
 * set, the user filter is locked to that user and the per-user endpoint is used. Filters and the page live in the
 * query string; a filter change replaces the history entry and paging pushes one (S1: the router leaves both alone).
 */
@customElement('user-activity-view')
export class UserActivityViewElement extends UmbLitElement {
  /** Locks the view to one user (the per-user tab). */
  @property({ type: String, attribute: 'user-key' }) userKey?: string;
  @property({ type: String }) heading?: string;

  @state() private _filters: ActivityFilters = filtersFromSearch('');
  @state() private _options?: ActivityFilterOptionsResponseModel;
  @state() private _page?: ActivityPageResponseModel;
  @state() private _loading = true;
  @state() private _forbidden = false;

  #request = 0;
  #onPopState = () => this.#readUrl();

  override connectedCallback() {
    super.connectedCallback();
    window.addEventListener('popstate', this.#onPopState);
    // Options first: the default window comes from the server's DefaultRangeDays, so the first list must wait for it.
    void this.#loadOptions().then(() => this.#readUrl());
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    window.removeEventListener('popstate', this.#onPopState);
  }

  protected override updated(changed: Map<string, unknown>) {
    if (changed.has('userKey') && changed.get('userKey') !== undefined) {
      // The workspace switched to another user: start that user's view from page 1.
      this._filters = { ...this._filters, page: 1 };
      this.#writeUrl(this._filters, false);
      void this.#load();
    }
  }

  get #localize(): Localize {
    return makeLocalize(this.localize);
  }

  #readUrl() {
    this._filters = { ...filtersFromSearch(window.location.search), userKey: this.userKey ?? filtersFromSearch(window.location.search).userKey };
    void this.#load();
  }

  async #loadOptions() {
    const { data, error } = await userActivityApi.filterOptions(this);
    if (error) {
      this._forbidden = (error as { status?: number }).status === 403;
      this._loading = false;
      return;
    }
    this._options = data;
  }

  #writeUrl(filters: ActivityFilters, push: boolean) {
    // A locked user is part of the route, not the query string.
    const forUrl = this.userKey ? { ...filters, userKey: undefined } : filters;
    const search = filtersToSearch(forUrl);
    const url = `${window.location.pathname}${search ? `?${search}` : ''}${window.location.hash}`;
    if (url === `${window.location.pathname}${window.location.search}${window.location.hash}`) return;
    if (push) window.history.pushState(null, '', url);
    else window.history.replaceState(null, '', url);
  }

  async #load() {
    if (this._forbidden) return;
    const requestId = ++this.#request;
    this._loading = true;
    const request = buildRequest(this._filters, this.userKey, new Date(), PAGE_SIZE, this._options?.defaultRangeDays);
    const response =
      request.kind === 'user' ? await userActivityApi.listForUser(this, request.userKey, request.query) : await userActivityApi.list(this, request.query);
    // A slower, older response must never overwrite a newer one.
    if (requestId !== this.#request) return;
    this._loading = false;
    if (response.error) {
      this._forbidden = (response.error as { status?: number }).status === 403;
      return;
    }
    this._forbidden = false;
    this._page = response.data;
  }

  #onFilters(event: Event) {
    const next = (event as CustomEvent<ActivityFilters>).detail;
    this._filters = { ...next, userKey: this.userKey ?? next.userKey };
    this.#writeUrl(this._filters, false);
    void this.#load();
  }

  #onPage(event: Event) {
    const { page } = (event as CustomEvent<{ page: number }>).detail;
    this._filters = { ...this._filters, page };
    this.#writeUrl(this._filters, true);
    void this.#load();
  }

  async #onOpen(event: Event) {
    const { source, id } = (event as CustomEvent<{ source: string; id: string }>).detail;
    try {
      await umbOpenModal(this, ACTIVITY_DETAIL_MODAL, { data: { source, id, sourceKeys: this.#sourceKeys() } });
    } catch {
      // Closing the sidebar rejects the modal promise; that is the normal way out.
    }
    this.renderRoot.querySelector<ActivityTableElement>('activity-table')?.focusRow(source, id);
  }

  async #onExport() {
    const request = buildRequest(this._filters, this.userKey, new Date(), PAGE_SIZE, this._options?.defaultRangeDays);
    const { skip: _skip, take: _take, order: _order, ...rest } = request.query;
    const query = request.kind === 'user' ? { ...rest, userKey: request.userKey } : rest;
    const { data, error } = await userActivityApi.exportCsv(this, query);
    if (error || !data) return;
    const url = URL.createObjectURL(data as unknown as Blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `user-activity-${new Date().toISOString().slice(0, 16).replace(/[-:T]/g, '').replace(/^(\d{8})/, '$1-')}.csv`;
    link.click();
    // Revoking straight away can cancel the download in some browsers.
    setTimeout(() => URL.revokeObjectURL(url), 10_000);
  }

  #filterText(): string {
    const f = this._filters;
    const label = (key: string, fallback: string) => term(this.#localize, key, fallback);
    const sources = (this._options?.sources ?? []).filter((s) => f.sources.includes(s.alias)).map((s) => label(s.displayNameKey, s.alias));
    return [
      f.search.trim() ? `“${f.search.trim()}”` : '',
      ...sources,
      ...f.categories.map((c) => label(`userActivity_category_${c}`, c)),
      ...f.entityTypes.map((e) => label(`userActivity_entityType_${e}`, e)),
    ]
      .filter(Boolean)
      .join(', ');
  }

  #sourceKeys() {
    return Object.fromEntries((this._options?.sources ?? []).map((s) => [s.alias, s.displayNameKey]));
  }

  override render() {
    if (this._forbidden) return html`<uui-box><p>${this.localize.term('userActivity_noAccess')}</p></uui-box>`;
    const page = this._page;
    const locale = document.documentElement.lang || navigator.language;
    return html`
      ${this.heading ? html`<h2>${this.heading}</h2>` : nothing}
      <activity-filter-bar
        .filters=${this._filters}
        .options=${this._options}
        .localize=${this.#localize}
        ?locked-user=${!!this.userKey}
        @filters-change=${this.#onFilters}
        @export-csv=${this.#onExport}
      ></activity-filter-bar>
      <activity-states
        ?loading=${this._loading}
        ?empty=${!this._loading && !!page && page.items.length === 0}
        ?depth-capped=${page?.depthCapped ?? false}
        .status=${page?.sources ?? []}
        .sources=${this._options?.sources ?? []}
        .selectedSources=${this._filters.sources}
        filter-text=${this.#filterText()}
        .localize=${this.#localize}
      >
        ${hasActiveFilters(this._filters, !!this.userKey)
          ? html`<uui-button slot="actions" look="secondary" label=${this.localize.term('userActivity_clearFilters')} @click=${() => this.#onFilters(new CustomEvent('filters-change', { detail: clearFilters(this._filters, !!this.userKey) }))}
              >${this.localize.term('userActivity_clearFilters')}</uui-button
            >`
          : nothing}
      </activity-states>
      <activity-table
        .items=${page?.items ?? []}
        .sourceKeys=${this.#sourceKeys()}
        .localize=${this.#localize}
        .locale=${locale}
        .now=${new Date()}
        ?busy=${this._loading}
        ?hide-user=${!!this.userKey}
        @activity-open=${this.#onOpen}
      ></activity-table>
      <activity-pager
        .page=${this._filters.page}
        .take=${PAGE_SIZE}
        .count=${page?.items.length ?? 0}
        .total=${page?.total ?? null}
        ?approximate=${page ? !page.totalIsExact : false}
        .locale=${locale}
        .localize=${this.#localize}
        @page-change=${this.#onPage}
      ></activity-pager>
    `;
  }

  static override styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1, 24px);
    }
    activity-table,
    activity-states {
      margin-top: var(--uui-size-space-5, 15px);
    }
  `;
}

export default UserActivityViewElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-view': UserActivityViewElement;
  }
}
