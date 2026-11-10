import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { ActivitySourceOptionResponseModel, SourceStatusResponseModel } from '../api/user-activity.api.js';
import { term } from './format.js';
import { identityLocalize, type Localize } from './localize.js';

/** Loading, partial-result, depth-cap and empty states above the table (design 6.4). */
export class ActivityStatesElement extends LitElement {
  static override properties = {
    loading: { type: Boolean },
    empty: { type: Boolean },
    depthCapped: { type: Boolean, attribute: 'depth-capped' },
    status: { attribute: false },
    sources: { attribute: false },
    selectedSources: { attribute: false },
    /** The active filters in words, for the empty state. */
    filterText: { type: String, attribute: 'filter-text' },
    localize: { attribute: false },
  };

  declare loading: boolean;
  declare empty: boolean;
  declare depthCapped: boolean;
  declare status: SourceStatusResponseModel[];
  declare sources: ActivitySourceOptionResponseModel[];
  declare selectedSources: string[];
  declare filterText: string;
  declare localize: Localize;

  constructor() {
    super();
    this.loading = false;
    this.empty = false;
    this.depthCapped = false;
    this.status = [];
    this.sources = [];
    this.selectedSources = [];
    this.filterText = '';
    this.localize = identityLocalize;
  }

  #sourceName(alias: string) {
    const source = this.sources.find((s) => s.alias === alias);
    return source ? term(this.localize, source.displayNameKey, alias) : alias;
  }

  #partial() {
    const problems = this.status.filter((s) => s.status !== 'ok');
    if (!problems.length) return nothing;
    const list = problems
      .map((s) => `${this.#sourceName(s.alias)} (${term(this.localize, s.status === 'timedOut' ? 'userActivity_sourceTimedOut' : 'userActivity_sourceFailed', s.status)})`)
      .join(', ');
    return html`<uui-box data-state="partial" role="status"><uui-icon name="icon-alert"></uui-icon> ${this.localize('userActivity_partialWarning', list)}</uui-box>`;
  }

  #hints() {
    const shown = this.sources.filter(
      (s) => (s.retentionHintDays || s.retentionKeptForever) && (this.selectedSources.length === 0 || this.selectedSources.includes(s.alias)),
    );
    return shown.map((s) =>
      s.retentionKeptForever
        ? html`<p class="hint">${this.localize('userActivity_retentionKeptForever', this.#sourceName(s.alias))}</p>`
        : html`<p class="hint">${this.localize('userActivity_retentionHint', this.#sourceName(s.alias), s.retentionHintDays)}</p>`,
    );
  }

  #empty() {
    if (!this.empty || this.loading) return nothing;
    const detail = this.filterText ? this.localize('userActivity_emptyFilters', this.filterText) : this.localize('userActivity_emptyFiltersNone');
    return html`<div data-state="empty" class="empty">
      <p><strong>${term(this.localize, 'userActivity_empty', 'No activity found.')}</strong></p>
      <p>${detail}</p>
      ${this.#hints()}
      <slot name="actions"></slot>
    </div>`;
  }

  override render() {
    return html`<div aria-busy=${this.loading ? 'true' : 'false'}>
      ${this.loading ? html`<uui-loader-bar></uui-loader-bar>` : nothing} ${this.#partial()}
      ${this.depthCapped ? html`<uui-box data-state="depth" role="status">${this.localize('userActivity_depthCapped')}</uui-box>` : nothing} ${this.#empty()}
    </div>`;
  }

  static override styles = css`
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
}

if (!customElements.get('activity-states')) customElements.define('activity-states', ActivityStatesElement);

declare global {
  interface HTMLElementTagNameMap {
    'activity-states': ActivityStatesElement;
  }
}
