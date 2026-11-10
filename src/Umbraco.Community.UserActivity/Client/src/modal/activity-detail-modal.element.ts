import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { userActivityApi } from '../api/user-activity.api.js';
import type { ActivityEventDetailResponseModel } from '../api/user-activity.api.js';
import { actorDisplay, term } from '../components/format.js';
import { makeLocalize, type Localize } from '../components/localize.js';
import type { ActivityDetailModalData } from './activity-detail.modal-token.js';
import '../components/activity-detail.element.js';

/** The event sidebar (design 6.3). Loads the one event, which is also what writes the "viewed" audit row (SEC-2). */
@customElement('user-activity-detail-modal')
export class UserActivityDetailModalElement extends UmbModalBaseElement<ActivityDetailModalData, never> {
  @state() private _event?: ActivityEventDetailResponseModel;
  @state() private _failed = false;

  override async firstUpdated() {
    const data = this.data;
    if (!data) return;
    const { data: event, error } = await userActivityApi.detail(this, data.source, data.id);
    if (error || !event) {
      this._failed = true;
      return;
    }
    this._event = event;
  }

  get #localize(): Localize {
    return makeLocalize(this.localize);
  }

  #headline(): string {
    const e = this._event;
    if (!e) return this.localize.term('userActivity_activity');
    const action = term(this.#localize, `userActivity_category_${e.category}`, e.category);
    const item = e.entity?.name ?? '';
    return this.localize.term('userActivity_detailHeadline', action, item) || `${action} ${actorDisplay(e.actor, this.#localize)}`;
  }

  override render() {
    return html`<umb-body-layout headline=${this.#headline()}>
      ${this._failed ? html`<p>${this.localize.term('userActivity_loadFailed')}</p>` : nothing}
      ${this._event
        ? html`<activity-detail
            .event=${this._event}
            .localize=${this.#localize}
            .sourceKeys=${this.data?.sourceKeys ?? {}}
            .locale=${document.documentElement.lang || navigator.language}
          ></activity-detail>`
        : this._failed
          ? nothing
          : html`<uui-loader-bar></uui-loader-bar>`}
      <uui-button slot="actions" label=${this.localize.term('userActivity_close')} @click=${this._rejectModal}>${this.localize.term('userActivity_close')}</uui-button>
    </umb-body-layout>`;
  }

  static override styles = css`
    :host {
      display: block;
    }
  `;
}

export default UserActivityDetailModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-detail-modal': UserActivityDetailModalElement;
  }
}
