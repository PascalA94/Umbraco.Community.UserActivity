import { html as v, customElement as m } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as n } from "@umbraco-cms/backoffice/lit-element";
import "./activity-view.element-CFGH7Kmg.js";
var c = Object.getOwnPropertyDescriptor, u = (i, l, o, s) => {
  for (var e = s > 1 ? void 0 : s ? c(l, o) : l, t = i.length - 1, a; t >= 0; t--)
    (a = i[t]) && (e = a(e) || e);
  return e;
};
let r = class extends n {
  render() {
    return v`<user-activity-view></user-activity-view>`;
  }
};
r = u([
  m("user-activity-global-view")
], r);
const y = r;
export {
  r as UserActivityGlobalViewElement,
  y as default
};
