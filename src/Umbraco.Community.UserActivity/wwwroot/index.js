const i = "user-activity-root", t = "UserActivity.Workspace.Root", a = "Umb.Workspace.User", e = "UserActivity.Condition.CanView", c = [
  {
    type: "localization",
    alias: "UserActivity.Localization.En",
    name: "User Activity English",
    meta: { culture: "en" },
    js: () => import("./en-CGJOswPr.js")
  },
  {
    // en-GB users get the same copy; the package ships English only in 0.1 (design 6.5).
    type: "localization",
    alias: "UserActivity.Localization.EnGb",
    name: "User Activity English (UK)",
    meta: { culture: "en-gb" },
    js: () => import("./en-CGJOswPr.js")
  },
  {
    type: "menuItem",
    alias: "UserActivity.MenuItem.Activity",
    name: "User Activity Menu Item",
    // After core's User Groups (100): weight 50.
    weight: 50,
    meta: { label: "#userActivity_activity", icon: "icon-time", entityType: i, menus: ["Umb.Menu.UserManagement"] }
  },
  {
    type: "workspace",
    kind: "default",
    alias: t,
    name: "User Activity Root Workspace",
    meta: { entityType: i, headline: "#userActivity_activity" }
  },
  {
    type: "workspaceView",
    alias: "UserActivity.WorkspaceView.Root.Activity",
    name: "User Activity Global View",
    element: () => import("./global-view.element-CGdKLPPQ.js"),
    weight: 100,
    meta: { label: "#userActivity_activity", pathname: "activity", icon: "icon-time" },
    conditions: [{ alias: "Umb.Condition.WorkspaceAlias", match: t }]
  },
  {
    type: "modal",
    alias: "UserActivity.Modal.Detail",
    name: "User Activity Detail Modal",
    element: () => import("./activity-detail-modal.element-B9HWBXBG.js")
  },
  {
    type: "condition",
    alias: e,
    name: "User Activity Can View Condition",
    api: () => import("./can-view.condition-Dx93uEkS.js")
  },
  {
    type: "workspaceView",
    alias: "UserActivity.WorkspaceView.User.Activity",
    name: "User Activity Tab",
    element: () => import("./user-tab.element-D-2AWKcZ.js"),
    // Core's Details view is weight 90 (S1), so this tab follows it.
    weight: 50,
    meta: { label: "#userActivity_activity", pathname: "activity", icon: "icon-time" },
    conditions: [
      { alias: "Umb.Condition.WorkspaceAlias", match: a },
      { alias: e }
    ]
  }
];
export {
  c as manifests
};
