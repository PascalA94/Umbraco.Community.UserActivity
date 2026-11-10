import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { ActivityEventDetailResponseModel } from '../api/user-activity.api.js';
import { actorDisplay, fullDate, itemHref, term } from './format.js';
import { identityLocalize, type Localize } from './localize.js';

/** Pretty JSON for a details value that is JSON, otherwise undefined. Output goes in a <pre> as text. */
function asJson(value: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) return undefined;
  try {
    return JSON.stringify(JSON.parse(trimmed), null, 2);
  } catch {
    return undefined;
  }
}

/**
 * The body of the event sidebar (design 6.3). Plain text bindings throughout: every value is other packages' or
 * other people's text. Details are free-form and may be absent (self-view users never receive them).
 */
export class ActivityDetailElement extends LitElement {
  static override properties = {
    event: { attribute: false },
    localize: { attribute: false },
    locale: { type: String },
    sourceKeys: { attribute: false },
  };

  declare event: ActivityEventDetailResponseModel | undefined;
  declare localize: Localize;
  declare locale: string | undefined;
  declare sourceKeys: Record<string, string>;

  constructor() {
    super();
    this.event = undefined;
    this.localize = identityLocalize;
    this.locale = undefined;
    this.sourceKeys = {};
  }

  #t(key: string, fallback = '') {
    return term(this.localize, key, fallback);
  }

  #row(label: string, value: unknown, data?: string) {
    return html`<div class="row" data-row=${data ?? nothing}><dt>${label}</dt><dd>${value}</dd></div>`;
  }

  override render() {
    const e = this.event;
    if (!e) return nothing;
    const via = e.actor.via;
    const defaultAdmin = e.actor.kind === 'defaultAdmin' || via?.kind === 'defaultAdmin';
    const entries = Object.entries(e.details ?? {});
    const href = itemHref(e.entity);
    const sourceName = Object.hasOwn(this.sourceKeys, e.source) ? this.#t(this.sourceKeys[e.source]!, e.source) : e.source;

    return html`
      <uui-box data-section="what" headline=${this.#t('userActivity_detailWhat', 'What')}>
        <dl>
          ${this.#row(this.#t('userActivity_detailCategory', 'Event type'), this.#t(`userActivity_category_${e.category}`, e.category))}
          ${this.#row(this.#t('userActivity_detailRawType', 'Raw type'), e.rawType)}
          ${this.#row(this.#t('userActivity_detailSource', 'Source'), sourceName)}
          ${this.#row(this.#t('userActivity_detailTimeUtc', 'Time (UTC)'), html`<time datetime=${e.occurredUtc}>${e.occurredUtc}</time>`)}
          ${this.#row(this.#t('userActivity_detailTimeLocal', 'Time (local)'), fullDate(e.occurredUtc, this.locale))}
          ${e.summary ? this.#row(this.#t('userActivity_detailSummary', 'Summary'), e.summary) : nothing}
        </dl>
      </uui-box>
      <uui-box data-section="who" headline=${this.#t('userActivity_detailWho', 'Who')}>
        <dl>
          ${this.#row(this.#t('userActivity_detailUser', 'User'), actorDisplay(e.actor, this.localize))}
          ${via ? this.#row(this.#t('userActivity_detailVia', 'Via'), actorDisplay(via, this.localize), 'via') : nothing}
          ${e.ipAddress ? this.#row(this.#t('userActivity_detailIp', 'IP address'), e.ipAddress, 'ip') : nothing}
        </dl>
        ${defaultAdmin ? html`<p data-note="default-admin">${this.#t('userActivity_detailDefaultAdminNote')}</p>` : nothing}
      </uui-box>
      ${e.entity
        ? html`<uui-box data-section="item" headline=${this.#t('userActivity_detailItem', 'Item')}>
            <dl>
              ${this.#row(this.#t('userActivity_detailItemType', 'Type'), this.#t(`userActivity_entityType_${e.entity.type}`, e.entity.type))}
              ${this.#row(this.#t('userActivity_detailItemName', 'Name'), e.entity.name ?? '')}
              ${this.#row(this.#t('userActivity_detailItemId', 'Key or id'), e.entity.key ?? e.entity.id ?? '')}
            </dl>
            ${href ? html`<uui-button look="secondary" href=${href} label=${this.#t('userActivity_detailOpen', 'Open')}>${this.#t('userActivity_detailOpen', 'Open')}</uui-button>` : nothing}
          </uui-box>`
        : nothing}
      ${entries.length
        ? html`<uui-box data-section="details" headline=${this.#t('userActivity_detailDetails', 'Details')}>
            <dl>
              ${entries.map(([key, value]) => {
                const json = asJson(value);
                return this.#row(key, json !== undefined ? html`<pre>${json}</pre>` : value);
              })}
            </dl>
          </uui-box>`
        : nothing}
    `;
  }

  static override styles = css`
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
}

if (!customElements.get('activity-detail')) customElements.define('activity-detail', ActivityDetailElement);

declare global {
  interface HTMLElementTagNameMap {
    'activity-detail': ActivityDetailElement;
  }
}
