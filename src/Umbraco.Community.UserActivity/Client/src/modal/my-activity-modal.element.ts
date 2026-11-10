import { css, customElement, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import type { MyActivityModalData } from './my-activity.modal-token.js';
import '../views/shared/activity-view.element.js';

/**
 * Your own activity, opened from your profile (QA-F3). The shared view locked to you, with filters kept in memory so
 * the page underneath keeps its URL.
 */
@customElement('user-activity-my-activity-modal')
export class UserActivityMyActivityModalElement extends UmbModalBaseElement<MyActivityModalData, never> {
  override render() {
    const userKey = this.data?.userKey;
    return html`<umb-body-layout headline=${this.localize.term('userActivity_myActivity')}>
      ${userKey ? html`<user-activity-view user-key=${userKey} no-url></user-activity-view>` : nothing}
      <uui-button slot="actions" label=${this.localize.term('userActivity_close')} @click=${this._rejectModal}>${this.localize.term('userActivity_close')}</uui-button>
    </umb-body-layout>`;
  }

  static override styles = css`
    :host {
      display: block;
    }
  `;
}

export default UserActivityMyActivityModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-my-activity-modal': UserActivityMyActivityModalElement;
  }
}
