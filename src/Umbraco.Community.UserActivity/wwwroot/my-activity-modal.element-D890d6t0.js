import { nothing as u, html as c, css as y, customElement as m } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement as n } from "@umbraco-cms/backoffice/modal";
import "./activity-view.element-Huo8OVpk.js";
var v = Object.getOwnPropertyDescriptor, d = (t, l, a, r) => {
  for (var e = r > 1 ? void 0 : r ? v(l, a) : l, s = t.length - 1, o; s >= 0; s--)
    (o = t[s]) && (e = o(e) || e);
  return e;
};
let i = class extends n {
  render() {
    const t = this.data?.userKey;
    return c`<umb-body-layout headline=${this.localize.term("userActivity_myActivity")}>
      ${t ? c`<user-activity-view user-key=${t} no-url></user-activity-view>` : u}
      <uui-button slot="actions" label=${this.localize.term("userActivity_close")} @click=${this._rejectModal}>${this.localize.term("userActivity_close")}</uui-button>
    </umb-body-layout>`;
  }
};
i.styles = y`
    :host {
      display: block;
    }
  `;
i = d([
  m("user-activity-my-activity-modal")
], i);
const p = i;
export {
  i as UserActivityMyActivityModalElement,
  p as default
};
