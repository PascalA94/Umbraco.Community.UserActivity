import { html as o, customElement as u } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as c } from "@umbraco-cms/backoffice/lit-element";
function m(e) {
  return `${e} is installed.`;
}
var v = Object.getOwnPropertyDescriptor, d = (e, s, l, n) => {
  for (var t = n > 1 ? void 0 : n ? v(s, l) : s, r = e.length - 1, a; r >= 0; r--)
    (a = e[r]) && (t = a(t) || t);
  return t;
};
let i = class extends c {
  render() {
    return o`<uui-box headline="User Activity"><p>${m("User Activity")}</p></uui-box>`;
  }
};
i = d([
  u("user-activity-dashboard")
], i);
const f = i;
export {
  i as UserActivityDashboardElement,
  f as default
};
