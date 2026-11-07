const t = {
  type: "dashboard",
  alias: "UserActivity.Dashboard",
  name: "User Activity Dashboard",
  element: () => import("./dashboard.element-Bo2MfGNf.js"),
  weight: -10,
  meta: {
    label: "User Activity",
    pathname: "user-activity"
  },
  conditions: [
    {
      alias: "Umb.Condition.SectionAlias",
      match: "Umb.Section.Content"
    }
  ]
}, a = [t];
export {
  a as manifests
};
