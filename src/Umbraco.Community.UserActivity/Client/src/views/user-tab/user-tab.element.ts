import { customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_USER_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/user';
import '../shared/activity-view.element.js';

/** The Activity tab on the user workspace (design 6.2): the shared view locked to the workspace's user. */
@customElement('user-activity-user-tab')
export class UserActivityUserTabElement extends UmbLitElement {
  @state() private _userKey?: string;
  @state() private _name?: string;

  constructor() {
    super();
    this.consumeContext(UMB_USER_WORKSPACE_CONTEXT, (workspace) => {
      if (!workspace) return;
      this.observe(workspace.unique, (unique) => (this._userKey = unique ?? undefined), '_userActivityKey');
      this.observe(workspace.name, (name) => (this._name = name ?? undefined), '_userActivityName');
    });
  }

  override render() {
    if (!this._userKey) return nothing;
    const heading = this._name ? this.localize.term('userActivity_activityFor', this._name) : this.localize.term('userActivity_activity');
    // The view resets to page 1 itself when user-key changes (workspace switched to another user).
    return html`<user-activity-view user-key=${this._userKey} heading=${heading}></user-activity-view>`;
  }
}

export default UserActivityUserTabElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-user-tab': UserActivityUserTabElement;
  }
}
