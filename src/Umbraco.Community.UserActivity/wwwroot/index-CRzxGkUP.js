const c = "UserActivity.Modal.MyActivity", t = "user-activity-root", i = "UserActivity.Workspace.Root", s = "Umb.Workspace.User", e = "UserActivity.Condition.CanView", o = [
  {
    type: "localization",
    alias: "UserActivity.Localization.En",
    name: "User Activity English",
    meta: { culture: "en" },
    js: () => import("./en-BzP2d8Uy.js")
  },
  {
    // en-GB users get the same copy; the package ships English only in 0.1 (design 6.5).
    type: "localization",
    alias: "UserActivity.Localization.EnGb",
    name: "User Activity English (UK)",
    meta: { culture: "en-gb" },
    js: () => import("./en-BzP2d8Uy.js")
  },
  {
    type: "menuItem",
    alias: "UserActivity.MenuItem.Activity",
    name: "User Activity Menu Item",
    // After core's User Groups (100): weight 50.
    weight: 50,
    meta: { label: "#userActivity_activity", icon: "icon-time", entityType: t, menus: ["Umb.Menu.UserManagement"] }
  },
  {
    type: "workspace",
    kind: "default",
    alias: i,
    name: "User Activity Root Workspace",
    meta: { entityType: t, headline: "#userActivity_activity" }
  },
  {
    type: "workspaceView",
    alias: "UserActivity.WorkspaceView.Root.Activity",
    name: "User Activity Global View",
    element: () => import("./global-view.element-c-wdCzyI.js"),
    weight: 100,
    meta: { label: "#userActivity_activity", pathname: "activity", icon: "icon-time" },
    conditions: [{ alias: "Umb.Condition.WorkspaceAlias", match: i }]
  },
  {
    type: "modal",
    alias: "UserActivity.Modal.Detail",
    name: "User Activity Detail Modal",
    element: () => import("./activity-detail-modal.element-B9HWBXBG.js")
  },
  {
    type: "modal",
    alias: c,
    name: "User Activity Your Activity Modal",
    element: () => import("./my-activity-modal.element-BCi2tjEX.js")
  },
  {
    // The route to your own activity for people without the Users section (QA-F3). Shown only when the API would
    // answer; the API still decides.
    type: "userProfileApp",
    alias: "UserActivity.UserProfileApp.Activity",
    name: "User Activity Profile App",
    // Core's profile apps use 100-1000; a low weight puts this box last, after core's own.
    weight: 10,
    element: () => import("./my-activity-profile-app.element-CALh1mbY.js"),
    meta: { label: "#userActivity_myActivity", pathname: "activity" }
  },
  {
    type: "condition",
    alias: e,
    name: "User Activity Can View Condition",
    api: () => import("./can-view.condition-7gAvEVqZ.js").then((a) => a.c)
  },
  {
    type: "workspaceView",
    alias: "UserActivity.WorkspaceView.User.Activity",
    name: "User Activity Tab",
    element: () => import("./user-tab.element-C-30HWY7.js"),
    // Core's Details view is weight 90 (S1), so this tab follows it.
    weight: 50,
    meta: { label: "#userActivity_activity", pathname: "activity", icon: "icon-time" },
    conditions: [
      { alias: "Umb.Condition.WorkspaceAlias", match: s },
      { alias: e }
    ]
  }
];
export {
  c as M,
  o as m
};
