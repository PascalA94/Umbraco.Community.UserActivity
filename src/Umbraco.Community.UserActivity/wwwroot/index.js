const i = "user-activity-root", t = "UserActivity.Workspace.Root", c = "Umb.Workspace.User", e = "UserActivity.Condition.CanView", o = [
  {
    type: "localization",
    alias: "UserActivity.Localization.En",
    name: "User Activity English",
    meta: { culture: "en" },
    js: () => import("./en-DPxXNrFP.js")
  },
  {
    // en-GB users get the same copy; the package ships English only in 0.1 (design 6.5).
    type: "localization",
    alias: "UserActivity.Localization.EnGb",
    name: "User Activity English (UK)",
    meta: { culture: "en-gb" },
    js: () => import("./en-DPxXNrFP.js")
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
    element: () => import("./global-view.element-O5DGN7fP.js"),
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
    type: "modal",
    alias: "UserActivity.Modal.MyActivity",
    name: "User Activity Your Activity Modal",
    element: () => import("./my-activity-modal.element-D890d6t0.js")
  },
  {
    // The route to your own activity for people without the Users section (QA-F3). Shown only when the API would
    // answer; the API still decides.
    type: "userProfileApp",
    alias: "UserActivity.UserProfileApp.Activity",
    name: "User Activity Profile App",
    element: () => import("./my-activity-profile-app.element-BZiKvLgK.js"),
    meta: { label: "#userActivity_myActivity", pathname: "activity" }
  },
  {
    type: "condition",
    alias: e,
    name: "User Activity Can View Condition",
    api: () => import("./can-view.condition-s--VEVqe.js").then((a) => a.c)
  },
  {
    type: "workspaceView",
    alias: "UserActivity.WorkspaceView.User.Activity",
    name: "User Activity Tab",
    element: () => import("./user-tab.element-CbnnZp31.js"),
    // Core's Details view is weight 90 (S1), so this tab follows it.
    weight: 50,
    meta: { label: "#userActivity_activity", pathname: "activity", icon: "icon-time" },
    conditions: [
      { alias: "Umb.Condition.WorkspaceAlias", match: c },
      { alias: e }
    ]
  }
];
export {
  o as manifests
};
