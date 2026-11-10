import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbOpenModal } from '@umbraco-cms/backoffice/modal';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { access } from '../conditions/can-view.condition.js';
import { profileVisible } from '../conditions/can-view.js';
import { MY_ACTIVITY_MODAL } from '../modal/my-activity.modal-token.js';

/**
 * "Your activity" in the current user's profile panel. Users without the Users section can't open a user workspace,
 * so this is their route to their own tab (QA-F3). Shown to readers and self-view users; the API decides.
 */
@customElement('user-activity-my-activity-profile-app')
export class UserActivityMyActivityProfileAppElement extends UmbLitElement {
  @state() private _userKey?: string;
  @state() private _visible = false;

  constructor() {
    super();
    void access(this).then((result) => (this._visible = profileVisible(result)));
    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (current) => {
      if (!current) return;
      this.observe(current.unique, (unique) => (this._userKey = unique ?? undefined), '_userActivityCurrentUser');
    });
  }

  async #open() {
    if (!this._userKey) return;
    try {
      await umbOpenModal(this, MY_ACTIVITY_MODAL, { data: { userKey: this._userKey } });
    } catch {
      // Closing the sidebar rejects the modal promise; that is the normal way out.
    }
  }

  override render() {
    if (!this._visible || !this._userKey) return nothing;
    return html`<uui-box headline=${this.localize.term('userActivity_myActivity')}>
      <p>${this.localize.term('userActivity_myActivityHint')}</p>
      <uui-button look="secondary" label=${this.localize.term('userActivity_openMyActivity')} @click=${this.#open}>
        <uui-icon name="icon-time"></uui-icon> ${this.localize.term('userActivity_openMyActivity')}
      </uui-button>
    </uui-box>`;
  }

  static override styles = css`
    p {
      margin-top: 0;
    }
  `;
}

export default UserActivityMyActivityProfileAppElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-my-activity-profile-app': UserActivityMyActivityProfileAppElement;
  }
}
