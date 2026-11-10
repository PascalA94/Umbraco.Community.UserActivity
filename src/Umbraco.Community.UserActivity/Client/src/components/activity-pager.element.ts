import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { term } from './format.js';
import { identityLocalize, type Localize } from './localize.js';

/** uui-pagination plus a polite live region announcing "Showing 51 to 100 of 1,234 events" (design 6.1, 6.5). */
export class ActivityPagerElement extends LitElement {
  static override properties = {
    page: { type: Number },
    take: { type: Number },
    /** Rows on the current page. */
    count: { type: Number },
    total: { attribute: false },
    approximate: { type: Boolean },
    loading: { type: Boolean },
    locale: { type: String },
    localize: { attribute: false },
  };

  declare page: number;
  declare take: number;
  declare count: number;
  declare total: number | null;
  /** The total is a lower bound (a source could not count). */
  declare approximate: boolean;
  declare loading: boolean;
  declare locale: string | undefined;
  declare localize: Localize;

  constructor() {
    super();
    this.page = 1;
    this.take = 50;
    this.count = 0;
    this.total = null;
    this.approximate = false;
    this.loading = false;
    this.locale = undefined;
    this.localize = identityLocalize;
  }

  #onChange(event: Event) {
    const page = (event.target as unknown as { current: number }).current;
    if (page !== this.page) this.dispatchEvent(new CustomEvent('page-change', { detail: { page }, bubbles: true, composed: true }));
  }

  /** One persistent polite region (WCAG 4.1.3): a region inserted already holding text is often not announced. */
  #status(): string {
    if (this.loading) return this.localize('userActivity_loading');
    if (this.count === 0) return this.localize('userActivity_empty');
    const number = new Intl.NumberFormat(this.locale);
    const first = (this.page - 1) * this.take + 1;
    const last = first + this.count - 1;
    const key = !this.approximate ? 'userActivity_showingRange' : 'userActivity_showingRangeAtLeast';
    return this.localize(key, number.format(first), number.format(last), number.format(this.total ?? last));
  }

  override render() {
    const pages = this.count === 0 ? 1 : Math.max(1, Math.ceil((this.total ?? this.page * this.take) / this.take));
    return html`<div class="pager">
      <div aria-live="polite">${this.#status()}</div>
      ${pages > 1 ? html`<uui-pagination label=${term(this.localize, 'userActivity_pagination', 'Pagination')} .total=${pages} .current=${this.page} @change=${this.#onChange}></uui-pagination>` : nothing}
    </div>`;
  }

  static override styles = css`
    .pager {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--uui-size-space-3, 9px);
      margin-top: var(--uui-size-space-5, 15px);
    }
  `;
}

if (!customElements.get('activity-pager')) customElements.define('activity-pager', ActivityPagerElement);

declare global {
  interface HTMLElementTagNameMap {
    'activity-pager': ActivityPagerElement;
  }
}
