const d = (t) => t, m = "userActivity_";
function l(t) {
  return (e, ...a) => e.startsWith(m) ? t.term(e, ...a) : e;
}
const c = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i, i = "/umbraco/section";
function f(t, e, a) {
  const n = t(e);
  return n && n !== e ? n : a;
}
const s = {
  system: "userActivity_actorSystem",
  defaultAdmin: "userActivity_actorDefaultAdmin",
  unknown: "userActivity_actorUnknown",
  automation: "userActivity_actorAutomation",
  serviceAccount: "userActivity_actorServiceAccount"
};
function v(t, e) {
  const a = t.name || t.storedName;
  if (a) return a;
  const n = Object.hasOwn(s, t.kind) ? s[t.kind] : void 0;
  return n ? f(e, n, t.label) : t.label;
}
function A(t) {
  return t && c.test(t) ? `${i}/user-management/workspace/user/edit/${t}/view/activity` : void 0;
}
const u = {
  document: (t) => `${i}/content/workspace/document/edit/${t}`,
  media: (t) => `${i}/media/workspace/media/edit/${t}`
};
function h(t) {
  if (!t?.exists || !t.key || !c.test(t.key)) return;
  const e = t.type.toLowerCase();
  return Object.hasOwn(u, e) ? u[e](t.key) : void 0;
}
function w(t, e, a) {
  const n = Math.round((new Date(t).getTime() - e.getTime()) / 1e3), r = new Intl.RelativeTimeFormat(a, { numeric: "auto" }), o = Math.abs(n);
  return o < 60 ? r.format(n, "second") : o < 3600 ? r.format(Math.round(n / 60), "minute") : o < 86400 ? r.format(Math.round(n / 3600), "hour") : o < 86400 * 30 ? r.format(Math.round(n / 86400), "day") : o < 86400 * 365 ? r.format(Math.round(n / (86400 * 30)), "month") : r.format(Math.round(n / (86400 * 365)), "year");
}
function y(t, e) {
  return new Date(t).toLocaleString(e, { dateStyle: "full", timeStyle: "long" });
}
export {
  h as a,
  v as b,
  y as f,
  d as i,
  l as m,
  w as r,
  f as t,
  A as u
};
