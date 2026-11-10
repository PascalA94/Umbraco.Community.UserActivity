import { customElement, html } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import '../shared/activity-view.element.js';

/** The Activity workspace view under Users (design 6.1): the shared view with no locked user. */
@customElement('user-activity-global-view')
export class UserActivityGlobalViewElement extends UmbLitElement {
  override render() {
    return html`<user-activity-view></user-activity-view>`;
  }
}

export default UserActivityGlobalViewElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-global-view': UserActivityGlobalViewElement;
  }
}
