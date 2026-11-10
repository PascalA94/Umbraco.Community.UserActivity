import { umbHttpClient as K } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as z } from "@umbraco-cms/backoffice/resources";
const X = {
  bodySerializer: (e) => JSON.stringify(e, (t, r) => typeof r == "bigint" ? r.toString() : r)
};
function Y({
  onRequest: e,
  onSseError: t,
  onSseEvent: r,
  responseTransformer: a,
  responseValidator: o,
  sseDefaultRetryDelay: c,
  sseMaxRetryAttempts: n,
  sseMaxRetryDelay: s,
  sseSleepFn: i,
  url: m,
  ...u
}) {
  let l;
  const w = i ?? ((f) => new Promise((h) => setTimeout(h, f)));
  return { stream: async function* () {
    let f = c ?? 3e3, h = 0;
    const g = u.signal ?? new AbortController().signal;
    for (; !g.aborted; ) {
      h++;
      const q = u.headers instanceof Headers ? u.headers : new Headers(u.headers);
      l !== void 0 && q.set("Last-Event-ID", l);
      try {
        const E = {
          redirect: "follow",
          ...u,
          body: u.serializedBody,
          headers: q,
          signal: g
        };
        let x = new Request(m, E);
        e && (x = await e(m, E));
        const y = await (u.fetch ?? globalThis.fetch)(x);
        if (!y.ok) throw new Error(`SSE failed: ${y.status} ${y.statusText}`);
        if (!y.body) throw new Error("No body in SSE response");
        const v = y.body.pipeThrough(new TextDecoderStream()).getReader();
        let p = "";
        const I = () => {
          try {
            v.cancel();
          } catch {
          }
        };
        g.addEventListener("abort", I);
        try {
          for (; ; ) {
            const { done: _, value: G } = await v.read();
            if (_) break;
            p += G, p = p.replace(/\r\n?/g, `
`);
            const U = p.split(`

`);
            p = U.pop() ?? "";
            for (const M of U) {
              const Q = M.split(`
`), C = [];
              let B;
              for (const S of Q)
                if (S.startsWith("data:"))
                  C.push(S.replace(/^data:\s*/, ""));
                else if (S.startsWith("event:"))
                  B = S.replace(/^event:\s*/, "");
                else if (S.startsWith("id:"))
                  l = S.replace(/^id:\s*/, "");
                else if (S.startsWith("retry:")) {
                  const R = Number.parseInt(S.replace(/^retry:\s*/, ""), 10);
                  Number.isNaN(R) || (f = R);
                }
              let j, N = !1;
              if (C.length) {
                const S = C.join(`
`);
                try {
                  j = JSON.parse(S), N = !0;
                } catch {
                  j = S;
                }
              }
              N && (o && await o(j), a && (j = await a(j))), r?.({
                data: j,
                event: B,
                id: l,
                retry: f
              }), C.length && (yield j);
            }
          }
        } finally {
          g.removeEventListener("abort", I), v.releaseLock();
        }
        break;
      } catch (E) {
        if (t?.(E), n !== void 0 && h >= n)
          break;
        const x = Math.min(f * 2 ** (h - 1), s ?? 3e4);
        await w(x);
      }
    }
  }() };
}
const Z = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, ee = (e) => {
  switch (e) {
    case "form":
      return ",";
    case "pipeDelimited":
      return "|";
    case "spaceDelimited":
      return "%20";
    default:
      return ",";
  }
}, te = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, W = ({
  allowReserved: e,
  explode: t,
  name: r,
  style: a,
  value: o
}) => {
  if (!t) {
    const s = (e ? o : o.map((i) => encodeURIComponent(i))).join(ee(a));
    switch (a) {
      case "label":
        return `.${s}`;
      case "matrix":
        return `;${r}=${s}`;
      case "simple":
        return s;
      default:
        return `${r}=${s}`;
    }
  }
  const c = Z(a), n = o.map((s) => a === "label" || a === "simple" ? e ? s : encodeURIComponent(s) : $({
    allowReserved: e,
    name: r,
    value: s
  })).join(c);
  return a === "label" || a === "matrix" ? c + n : n;
}, $ = ({
  allowReserved: e,
  name: t,
  value: r
}) => {
  if (r == null)
    return "";
  if (typeof r == "object")
    throw new Error(
      "Deeply-nested arrays/objects aren’t supported. Provide your own `querySerializer()` to handle these."
    );
  return `${t}=${e ? r : encodeURIComponent(r)}`;
}, V = ({
  allowReserved: e,
  explode: t,
  name: r,
  style: a,
  value: o,
  valueOnly: c
}) => {
  if (o instanceof Date)
    return c ? o.toISOString() : `${r}=${o.toISOString()}`;
  if (a !== "deepObject" && !t) {
    let i = [];
    Object.entries(o).forEach(([u, l]) => {
      i = [...i, u, e ? l : encodeURIComponent(l)];
    });
    const m = i.join(",");
    switch (a) {
      case "form":
        return `${r}=${m}`;
      case "label":
        return `.${m}`;
      case "matrix":
        return `;${r}=${m}`;
      default:
        return m;
    }
  }
  const n = te(a), s = Object.entries(o).map(
    ([i, m]) => $({
      allowReserved: e,
      name: a === "deepObject" ? `${r}[${i}]` : i,
      value: m
    })
  ).join(n);
  return a === "label" || a === "matrix" ? n + s : s;
}, re = /\{[^{}]+\}/g, se = ({ path: e, url: t }) => {
  let r = t;
  const a = t.match(re);
  if (a)
    for (const o of a) {
      let c = !1, n = o.substring(1, o.length - 1), s = "simple";
      n.endsWith("*") && (c = !0, n = n.substring(0, n.length - 1)), n.startsWith(".") ? (n = n.substring(1), s = "label") : n.startsWith(";") && (n = n.substring(1), s = "matrix");
      const i = e[n];
      if (i == null)
        continue;
      if (Array.isArray(i)) {
        r = r.replace(o, W({ explode: c, name: n, style: s, value: i }));
        continue;
      }
      if (typeof i == "object") {
        r = r.replace(
          o,
          V({
            explode: c,
            name: n,
            style: s,
            value: i,
            valueOnly: !0
          })
        );
        continue;
      }
      if (s === "matrix") {
        r = r.replace(
          o,
          `;${$({
            name: n,
            value: i
          })}`
        );
        continue;
      }
      const m = encodeURIComponent(
        s === "label" ? `.${i}` : i
      );
      r = r.replace(o, m);
    }
  return r;
}, ae = ({
  baseUrl: e,
  path: t,
  query: r,
  querySerializer: a,
  url: o
}) => {
  const c = o.startsWith("/") ? o : `/${o}`;
  let n = (e ?? "") + c;
  t && (n = se({ path: t, url: n }));
  let s = r ? a(r) : "";
  return s.startsWith("?") && (s = s.substring(1)), s && (n += `?${s}`), n;
};
function D(e) {
  const t = e.body !== void 0;
  if (t && e.bodySerializer)
    return "serializedBody" in e ? e.serializedBody !== void 0 && e.serializedBody !== "" ? e.serializedBody : null : e.body !== "" ? e.body : null;
  if (t)
    return e.body;
}
const ne = async (e, t) => {
  const r = typeof t == "function" ? await t(e) : t;
  if (r)
    return e.scheme === "bearer" ? `Bearer ${r}` : e.scheme === "basic" ? `Basic ${btoa(r)}` : r;
}, L = ({
  parameters: e = {},
  ...t
} = {}) => (a) => {
  const o = [];
  if (a && typeof a == "object")
    for (const c in a) {
      const n = a[c];
      if (n == null)
        continue;
      const s = e[c] || t;
      if (Array.isArray(n)) {
        const i = W({
          allowReserved: s.allowReserved,
          explode: !0,
          name: c,
          style: "form",
          value: n,
          ...s.array
        });
        i && o.push(i);
      } else if (typeof n == "object") {
        const i = V({
          allowReserved: s.allowReserved,
          explode: !0,
          name: c,
          style: "deepObject",
          value: n,
          ...s.object
        });
        i && o.push(i);
      } else {
        const i = $({
          allowReserved: s.allowReserved,
          name: c,
          value: n
        });
        i && o.push(i);
      }
    }
  return o.join("&");
}, ie = (e) => {
  if (!e)
    return "stream";
  const t = e.split(";")[0]?.trim();
  if (t) {
    if (t.startsWith("application/json") || t.endsWith("+json"))
      return "json";
    if (t === "multipart/form-data")
      return "formData";
    if (["application/", "audio/", "image/", "video/"].some((r) => t.startsWith(r)))
      return "blob";
    if (t.startsWith("text/"))
      return "text";
  }
}, oe = (e, t) => t ? !!(e.headers.has(t) || e.query?.[t] || e.headers.get("Cookie")?.includes(`${t}=`)) : !1;
async function ce(e) {
  for (const t of e.security ?? []) {
    if (oe(e, t.name))
      continue;
    const r = await ne(t, e.auth);
    if (!r)
      continue;
    const a = t.name ?? "Authorization";
    switch (t.in) {
      case "query":
        e.query || (e.query = {}), e.query[a] = r;
        break;
      case "cookie":
        e.headers.append("Cookie", `${a}=${r}`);
        break;
      default:
        e.headers.set(a, r);
        break;
    }
  }
}
const P = (e) => ae({
  baseUrl: e.baseUrl,
  path: e.path,
  query: e.query,
  querySerializer: typeof e.querySerializer == "function" ? e.querySerializer : L(e.querySerializer),
  url: e.url
}), H = (e, t) => {
  const r = { ...e, ...t };
  return r.baseUrl?.endsWith("/") && (r.baseUrl = r.baseUrl.substring(0, r.baseUrl.length - 1)), r.headers = F(e.headers, t.headers), r;
}, le = (e) => {
  const t = [];
  return e.forEach((r, a) => {
    t.push([a, r]);
  }), t;
}, F = (...e) => {
  const t = new Headers();
  for (const r of e) {
    if (!r)
      continue;
    const a = r instanceof Headers ? le(r) : Object.entries(r);
    for (const [o, c] of a)
      if (c === null)
        t.delete(o);
      else if (Array.isArray(c))
        for (const n of c)
          t.append(o, n);
      else c !== void 0 && t.set(
        o,
        typeof c == "object" ? JSON.stringify(c) : c
      );
  }
  return t;
};
class T {
  constructor() {
    this.fns = [];
  }
  clear() {
    this.fns = [];
  }
  eject(t) {
    const r = this.getInterceptorIndex(t);
    this.fns[r] && (this.fns[r] = null);
  }
  exists(t) {
    const r = this.getInterceptorIndex(t);
    return !!this.fns[r];
  }
  getInterceptorIndex(t) {
    return typeof t == "number" ? this.fns[t] ? t : -1 : this.fns.indexOf(t);
  }
  update(t, r) {
    const a = this.getInterceptorIndex(t);
    return this.fns[a] ? (this.fns[a] = r, t) : !1;
  }
  use(t) {
    return this.fns.push(t), this.fns.length - 1;
  }
}
const ue = () => ({
  error: new T(),
  request: new T(),
  response: new T()
}), fe = L({
  allowReserved: !1,
  array: {
    explode: !0,
    style: "form"
  },
  object: {
    explode: !0,
    style: "deepObject"
  }
}), de = {
  "Content-Type": "application/json"
}, J = (e = {}) => ({
  ...X,
  headers: de,
  parseAs: "auto",
  querySerializer: fe,
  ...e
}), he = (e = {}) => {
  let t = H(J(), e);
  const r = () => ({ ...t }), a = (u) => (t = H(t, u), r()), o = ue(), c = async (u) => {
    const l = {
      ...t,
      ...u,
      fetch: u.fetch ?? t.fetch ?? globalThis.fetch,
      headers: F(t.headers, u.headers),
      serializedBody: void 0
    };
    l.security && await ce(l), l.requestValidator && await l.requestValidator(l), l.body !== void 0 && l.bodySerializer && (l.serializedBody = l.bodySerializer(l.body)), (l.body === void 0 || l.serializedBody === "") && l.headers.delete("Content-Type");
    const w = l, b = P(w);
    return { opts: w, url: b };
  }, n = async (u) => {
    const l = u.throwOnError ?? t.throwOnError, w = u.responseStyle ?? t.responseStyle;
    let b, d;
    try {
      const { opts: f, url: h } = await c(u), g = {
        redirect: "follow",
        ...f,
        body: D(f)
      };
      b = new Request(h, g);
      for (const y of o.request.fns)
        y && (b = await y(b, f));
      const q = f.fetch;
      d = await q(b);
      for (const y of o.response.fns)
        y && (d = await y(d, b, f));
      const E = {
        request: b,
        response: d
      };
      if (d.ok) {
        const y = (f.parseAs === "auto" ? ie(d.headers.get("Content-Type")) : f.parseAs) ?? "json";
        if (d.status === 204 || d.headers.get("Content-Length") === "0") {
          let p;
          switch (y) {
            case "arrayBuffer":
            case "blob":
            case "text":
              p = await d[y]();
              break;
            case "formData":
              p = new FormData();
              break;
            case "stream":
              p = d.body;
              break;
            default:
              p = {};
              break;
          }
          return f.responseStyle === "data" ? p : {
            data: p,
            ...E
          };
        }
        let v;
        switch (y) {
          case "arrayBuffer":
          case "blob":
          case "formData":
          case "text":
            v = await d[y]();
            break;
          case "json": {
            const p = await d.text();
            v = p ? JSON.parse(p) : {};
            break;
          }
          case "stream":
            return f.responseStyle === "data" ? d.body : {
              data: d.body,
              ...E
            };
        }
        return y === "json" && (f.responseValidator && await f.responseValidator(v), f.responseTransformer && (v = await f.responseTransformer(v))), f.responseStyle === "data" ? v : {
          data: v,
          ...E
        };
      }
      const x = await d.text();
      let k;
      try {
        k = JSON.parse(x);
      } catch {
      }
      throw k ?? x;
    } catch (f) {
      let h = f;
      for (const g of o.error.fns)
        g && (h = await g(h, d, b, u));
      if (h = h || {}, l)
        throw h;
      return w === "data" ? void 0 : {
        error: h,
        request: b,
        response: d
      };
    }
  }, s = (u) => (l) => n({ ...l, method: u }), i = (u) => async (l) => {
    const { opts: w, url: b } = await c(l);
    return Y({
      ...w,
      body: w.body,
      method: u,
      onRequest: async (d, f) => {
        let h = new Request(d, f);
        for (const g of o.request.fns)
          g && (h = await g(h, w));
        return h;
      },
      serializedBody: D(w),
      url: b
    });
  };
  return {
    buildUrl: (u) => P({ ...t, ...u }),
    connect: s("CONNECT"),
    delete: s("DELETE"),
    get: s("GET"),
    getConfig: r,
    head: s("HEAD"),
    interceptors: o,
    options: s("OPTIONS"),
    patch: s("PATCH"),
    post: s("POST"),
    put: s("PUT"),
    request: n,
    setConfig: a,
    sse: {
      connect: i("CONNECT"),
      delete: i("DELETE"),
      get: i("GET"),
      head: i("HEAD"),
      options: i("OPTIONS"),
      patch: i("PATCH"),
      post: i("POST"),
      put: i("PUT"),
      trace: i("TRACE")
    },
    trace: s("TRACE")
  };
}, O = he(J({ baseUrl: "https://localhost:44331/" })), ye = (e) => (e?.client ?? O).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/user-activity/events",
  ...e
}), pe = (e) => (e.client ?? O).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/user-activity/events/{source}/{id}",
  ...e
}), be = (e) => (e?.client ?? O).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/user-activity/events/export",
  ...e
}), me = (e) => (e?.client ?? O).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/user-activity/filter-options",
  ...e
}), ge = (e) => (e.client ?? O).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/user-activity/users/{userKey}/events",
  ...e
}), A = K, Se = {
  filterOptions: (e) => z(e, me({ client: A }), { disableNotifications: !0 }),
  list: (e, t) => z(e, ye({ client: A, query: t })),
  listForUser: (e, t, r) => z(e, ge({ client: A, path: { userKey: t }, query: r })),
  /** The CSV as a blob. It goes through the authenticated client, so a plain link would arrive without the token. */
  exportCsv: (e, t) => z(e, be({ client: A, query: t, parseAs: "blob" })),
  detail: (e, t, r) => z(e, pe({ client: A, path: { source: t, id: r } }))
};
export {
  Se as u
};
