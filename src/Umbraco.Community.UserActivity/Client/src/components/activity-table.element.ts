import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { ActivityEventResponseModel } from '../api/user-activity.api.js';
import { actorDisplay, fullDate, itemHref, relativeTime, term, userTabHref } from './format.js';
import { identityLocalize, type Localize } from './localize.js';
import './activity-source-tag.element.js';

/**
 * The activity table (design 6.1). Every cell is a Lit text binding: names, summaries and raw types are other
 * people's text and may be hostile (S3 [Critical]). Links are built from constants plus a validated GUID.
 * Built on uui-table (the same primitives core's tables use) so the row button and the labelled table are ours.
 */
export class ActivityTableElement extends LitElement {
  static override properties = {
    items: { attribute: false },
    sourceKeys: { attribute: false },
    localize: { attribute: false },
    busy: { type: Boolean },
    hideUser: { type: Boolean, attribute: 'hide-user' },
    locale: { type: String },
    now: { attribute: false },
  };

  declare items: ActivityEventResponseModel[];
  /** Source alias to its localisation key (from the filter options). */
  declare sourceKeys: Record<string, string>;
  declare localize: Localize;
  declare busy: boolean;
  /** Set on the per-user tab, where every row is the same user. A boolean attribute that is absent means false. */
  declare hideUser: boolean;
  declare locale: string | undefined;
  declare now: Date;

  constructor() {
    super();
    this.items = [];
    this.sourceKeys = {};
    this.localize = identityLocalize;
    this.busy = false;
    this.hideUser = false;
    this.locale = undefined;
    this.now = new Date();
  }

  #open(item: ActivityEventResponseModel) {
    this.dispatchEvent(new CustomEvent('activity-open', { detail: { source: item.source, id: item.id }, bubbles: true, composed: true }));
  }

  /** Puts focus back on a row's button, for when the detail sidebar closes (design 6.3). */
  focusRow(source: string, id: string) {
    const buttons = [...(this.renderRoot?.querySelectorAll<HTMLButtonElement>('button.open') ?? [])];
    buttons.find((b) => b.dataset.source === source && b.dataset.id === id)?.focus();
  }

  #t(key: string, fallback = '') {
    return term(this.localize, key, fallback);
  }

  #openLabel(item: ActivityEventResponseModel) {
    const when = relativeTime(item.occurredUtc, this.now, this.locale);
    const action = this.#t(`userActivity_category_${item.category}`, item.category);
    return this.localize('userActivity_openDetail', when, action, item.entity?.name ?? '');
  }

  #user(item: ActivityEventResponseModel) {
    const name = actorDisplay(item.actor, this.localize);
    const href = userTabHref(item.actor.userKey);
    const label = href ? html`<a href=${href}>${name}</a>` : html`<span>${name}</span>`;
    const via = item.actor.via ? html`<small class="via">${this.localize('userActivity_via', actorDisplay(item.actor.via, this.localize))}</small>` : nothing;
    return html`<div class="user"><uui-avatar name=${name}></uui-avatar><div>${label}${via}</div></div>`;
  }

  #item(item: ActivityEventResponseModel) {
    const entity = item.entity;
    if (!entity) return html`<span>${item.summary ?? ''}</span>`;
    const name = entity.name || entity.id || entity.key || '';
    const href = itemHref(entity);
    const text = href ? html`<a href=${href}>${name}</a>` : html`<span>${name}</span>`;
    return html`<div class="item"><small>${this.#t(`userActivity_entityType_${entity.type}`, entity.type)}</small>${text}</div>`;
  }

  #row(item: ActivityEventResponseModel) {
    return html`<uui-table-row>
      <uui-table-cell>
        <button type="button" class="open" data-source=${item.source} data-id=${item.id} aria-label=${this.#openLabel(item)} @click=${() => this.#open(item)}>
          <time datetime=${item.occurredUtc} title=${fullDate(item.occurredUtc, this.locale)}>${relativeTime(item.occurredUtc, this.now, this.locale)}</time>
        </button>
      </uui-table-cell>
      ${!this.hideUser ? html`<uui-table-cell>${this.#user(item)}</uui-table-cell>` : nothing}
      <uui-table-cell>${this.#t(`userActivity_category_${item.category}`, item.category)}</uui-table-cell>
      <uui-table-cell>${this.#item(item)}</uui-table-cell>
      <uui-table-cell>
        <activity-source-tag .alias=${item.source} .displayNameKey=${Object.hasOwn(this.sourceKeys, item.source) ? this.sourceKeys[item.source] : ''} .localize=${this.localize}></activity-source-tag>
      </uui-table-cell>
    </uui-table-row>`;
  }

  override render() {
    return html`<uui-table aria-label=${this.#t('userActivity_tableCaption', 'User activity, newest first')} aria-busy=${this.busy ? 'true' : 'false'}>
      <uui-table-head>
        <uui-table-head-cell>${this.#t('userActivity_columnWhen', 'When')}</uui-table-head-cell>
        ${!this.hideUser ? html`<uui-table-head-cell>${this.#t('userActivity_columnUser', 'User')}</uui-table-head-cell>` : nothing}
        <uui-table-head-cell>${this.#t('userActivity_columnAction', 'Action')}</uui-table-head-cell>
        <uui-table-head-cell>${this.#t('userActivity_columnItem', 'Item')}</uui-table-head-cell>
        <uui-table-head-cell>${this.#t('userActivity_columnSource', 'Source')}</uui-table-head-cell>
      </uui-table-head>
      ${this.items.map((item) => this.#row(item))}
    </uui-table>`;
  }

  static override styles = css`
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
}

if (!customElements.get('activity-table')) customElements.define('activity-table', ActivityTableElement);

declare global {
  interface HTMLElementTagNameMap {
    'activity-table': ActivityTableElement;
  }
}
