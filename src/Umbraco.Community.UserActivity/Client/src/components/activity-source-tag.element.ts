import { LitElement, css, html } from '@umbraco-cms/backoffice/external/lit';
import { identityLocalize, type Localize } from './localize.js';

/** A source's name as a tag. Text only: the name comes from a localisation key, never from event data. */
export class ActivitySourceTagElement extends LitElement {
  static override properties = {
    alias: { type: String },
    displayNameKey: { type: String, attribute: 'display-name-key' },
    localize: { attribute: false },
  };

  declare alias: string;
  declare displayNameKey: string;
  declare localize: Localize;

  constructor() {
    super();
    this.alias = '';
    this.displayNameKey = '';
    this.localize = identityLocalize;
  }

  override render() {
    const name = this.displayNameKey ? this.localize(this.displayNameKey) : '';
    // A third-party source without a translation shows its alias instead of the raw key.
    const label = name && name !== this.displayNameKey ? name : this.alias;
    return html`<uui-tag look="secondary">${label}</uui-tag>`;
  }

  static override styles = css`
    :host {
      display: inline-block;
    }
  `;
}

if (!customElements.get('activity-source-tag')) customElements.define('activity-source-tag', ActivitySourceTagElement);

declare global {
  interface HTMLElementTagNameMap {
    'activity-source-tag': ActivitySourceTagElement;
  }
}
