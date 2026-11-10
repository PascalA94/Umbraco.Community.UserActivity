import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { ActivityFilterOptionsResponseModel } from '../api/user-activity.api.js';
import { clearFilters, DEFAULT_RANGE_DAYS, hasActiveFilters, RANGE_PRESETS, type ActivityFilters, type RangePreset } from '../state/activity-filter-state.js';
import { term } from './format.js';
import { identityLocalize, type Localize } from './localize.js';

const SEARCH_DEBOUNCE_MS = 300;
type Choice = { value: string; label: string };

/**
 * The filter bar shared by the global view and the per-user tab (design 6.1, 6.2). It owns no state: it shows
 * `filters`, and every change leaves as a `filters-change` event carrying a new object with the page reset.
 */
export class ActivityFilterBarElement extends LitElement {
  static override properties = {
    filters: { attribute: false },
    options: { attribute: false },
    localize: { attribute: false },
    /** The per-user tab fixes the user, so no picker is shown. */
    lockedUser: { type: Boolean, attribute: 'locked-user' },
    _customOpen: { state: true },
  };

  declare filters: ActivityFilters;
  declare options: ActivityFilterOptionsResponseModel | undefined;
  declare localize: Localize;
  declare lockedUser: boolean;
  declare _customOpen: boolean;
  #searchTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    super();
    this.filters = { sources: [], categories: [], entityTypes: [], search: '', page: 1 };
    this.options = undefined;
    this.localize = identityLocalize;
    this.lockedUser = false;
    this._customOpen = false;
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    clearTimeout(this.#searchTimer);
  }

  #t(key: string, fallback = '') {
    return term(this.localize, key, fallback);
  }

  #emit(change: Partial<ActivityFilters>) {
    const next: ActivityFilters = { ...this.filters, ...change, page: 1 };
    this.dispatchEvent(new CustomEvent('filters-change', { detail: next, bubbles: true, composed: true }));
  }

  #onSearch(event: Event) {
    const value = (event.target as HTMLInputElement).value;
    clearTimeout(this.#searchTimer);
    this.#searchTimer = setTimeout(() => this.#emit({ search: value }), SEARCH_DEBOUNCE_MS);
  }

  #toggle(list: string[], value: string, checked: boolean) {
    return checked ? [...new Set([...list, value])] : list.filter((v) => v !== value);
  }

  #dropdown(id: string, label: string, choices: Choice[], selected: string[], apply: (next: string[]) => void) {
    const count = selected.length;
    return html`<div class="filter">
      <uui-button
        popovertarget=${id}
        look="outline"
        label=${this.localize('userActivity_filterCount', label, count)}
        aria-label=${this.localize('userActivity_filterCount', label, count)}
        >${this.localize('userActivity_filterCountBadge', label, count)}<uui-symbol-expand></uui-symbol-expand></uui-button
      >
      <uui-popover-container id=${id} placement="bottom-start">
        <div class="popover">
          ${choices.map(
            (choice) => html`<uui-checkbox
              label=${choice.label}
              .checked=${selected.includes(choice.value)}
              @change=${(e: Event) => apply(this.#toggle(selected, choice.value, (e.target as HTMLInputElement).checked))}
              >${choice.label}</uui-checkbox
            >`,
          )}
        </div>
      </uui-popover-container>
    </div>`;
  }

  /** The preset that is the server's default window; selecting it means "no range" so the URL stays clean. */
  #defaultPreset(): RangePreset {
    const days = this.options?.defaultRangeDays ?? DEFAULT_RANGE_DAYS;
    return ((Object.keys(RANGE_PRESETS) as RangePreset[]).find((k) => RANGE_PRESETS[k] === days) ?? '30d') as RangePreset;
  }

  #rangeValue(): string {
    if (this.filters.from || this.filters.to || this._customOpen) return 'custom';
    return this.filters.range ?? this.#defaultPreset();
  }

  #onRange(event: Event) {
    const value = (event.target as HTMLSelectElement).value;
    if (value === 'custom') {
      this._customOpen = true;
      return;
    }
    this._customOpen = false;
    this.#emit({ range: value === this.#defaultPreset() ? undefined : (value as RangePreset), from: undefined, to: undefined });
  }

  #rangeSelect() {
    const current = this.#rangeValue();
    const presets = (Object.keys(RANGE_PRESETS) as RangePreset[]).map((key) => ({ name: this.#t(`userActivity_range${key}`, key), value: key, selected: current === key }));
    const options = [...presets, { name: this.#t('userActivity_rangeCustom', 'Custom'), value: 'custom', selected: current === 'custom' }];
    return html`<uui-select label=${this.#t('userActivity_filterDate', 'Date range')} .options=${options} @change=${this.#onRange}></uui-select>`;
  }

  #dateInput(kind: 'from' | 'to') {
    const label = this.#t(kind === 'from' ? 'userActivity_dateFrom' : 'userActivity_dateTo', kind);
    return html`<uui-input
      type="date"
      label=${label}
      aria-label=${label}
      .value=${this.filters[kind] ?? ''}
      @change=${(e: Event) => this.#emit({ [kind]: (e.target as HTMLInputElement).value || undefined, range: undefined })}
    ></uui-input>`;
  }

  override render() {
    const options = this.options;
    const sources: Choice[] = (options?.sources ?? []).map((s) => ({ value: s.alias, label: term(this.localize, s.displayNameKey, s.alias) }));
    const categories: Choice[] = (options?.categories ?? []).map((c) => ({ value: c, label: this.#t(`userActivity_category_${c}`, c) }));
    const entityTypes: Choice[] = (options?.entityTypes ?? []).map((e) => ({ value: e, label: this.#t(`userActivity_entityType_${e}`, e) }));
    const custom = this.#rangeValue() === 'custom';

    return html`<div class="bar" role="search">
      <uui-input
        class="search"
        type="search"
        label=${this.#t('userActivity_filterSearch', 'Search activity')}
        placeholder=${this.#t('userActivity_filterSearch', 'Search activity')}
        maxlength="100"
        .value=${this.filters.search}
        @input=${this.#onSearch}
      ></uui-input>
      ${this.lockedUser
        ? nothing
        : html`<umb-user-input
            aria-label=${this.#t('userActivity_filterUser', 'User')}
            max="1"
            .selection=${this.filters.userKey ? [this.filters.userKey] : []}
            @change=${(e: Event) => this.#emit({ userKey: (e.target as unknown as { selection: string[] }).selection[0] })}
          ></umb-user-input>`}
      ${this.#dropdown('activity-filter-source', this.#t('userActivity_filterSource', 'Source'), sources, this.filters.sources, (next) => this.#emit({ sources: next }))}
      ${this.#dropdown('activity-filter-category', this.#t('userActivity_filterCategory', 'Event type'), categories, this.filters.categories, (next) => this.#emit({ categories: next }))}
      ${this.#dropdown('activity-filter-entity', this.#t('userActivity_filterEntityType', 'Item type'), entityTypes, this.filters.entityTypes, (next) => this.#emit({ entityTypes: next }))}
      ${this.#rangeSelect()} ${custom ? html`${this.#dateInput('from')}${this.#dateInput('to')}` : nothing}
      ${hasActiveFilters(this.filters, this.lockedUser)
        ? html`<uui-button look="secondary" label=${this.#t('userActivity_clearFilters', 'Clear filters')} @click=${() => this.#clear()}
            >${this.#t('userActivity_clearFilters', 'Clear filters')}</uui-button
          >`
        : nothing}
      ${options?.exportEnabled
        ? html`<uui-button data-action="export" look="secondary" label=${this.#t('userActivity_exportCsv', 'Download CSV')} @click=${() => this.dispatchEvent(new CustomEvent('export-csv', { bubbles: true, composed: true }))}
            >${this.#t('userActivity_exportCsv', 'Download CSV')}</uui-button
          >`
        : nothing}
    </div>`;
  }

  #clear() {
    this._customOpen = false;
    this.#emit(clearFilters(this.filters, this.lockedUser));
  }

  static override styles = css`
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
}

if (!customElements.get('activity-filter-bar')) customElements.define('activity-filter-bar', ActivityFilterBarElement);

declare global {
  interface HTMLElementTagNameMap {
    'activity-filter-bar': ActivityFilterBarElement;
  }
}
