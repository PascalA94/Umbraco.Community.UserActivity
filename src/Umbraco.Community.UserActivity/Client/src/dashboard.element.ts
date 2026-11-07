import { html, customElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { greeting } from './greeting.js';

@customElement('user-activity-dashboard')
export class UserActivityDashboardElement extends UmbLitElement {
  override render() {
    // Lit text bindings only: never raw HTML (build/check-xss.sh enforces it).
    return html`<uui-box headline="User Activity"><p>${greeting('User Activity')}</p></uui-box>`;
  }
}

export default UserActivityDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    'user-activity-dashboard': UserActivityDashboardElement;
  }
}
