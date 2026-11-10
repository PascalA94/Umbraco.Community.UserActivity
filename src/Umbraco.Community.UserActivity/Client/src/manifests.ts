import { MY_ACTIVITY_MODAL_ALIAS } from './modal/aliases.js';

// Aliases are permanent once released: every one is prefixed "UserActivity." (design 5).
// Workspace and menu aliases are the S1 spike's, identical on 17.8 and 18.3 (docs/spikes.md).
export const ROOT_ENTITY_TYPE = 'user-activity-root';
export const ROOT_WORKSPACE_ALIAS = 'UserActivity.Workspace.Root';
export const USER_WORKSPACE_ALIAS = 'Umb.Workspace.User';
export const CAN_VIEW_CONDITION = 'UserActivity.Condition.CanView';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'localization',
    alias: 'UserActivity.Localization.En',
    name: 'User Activity English',
    meta: { culture: 'en' },
    js: () => import('./localization/en.js'),
  },
  {
    // en-GB users get the same copy; the package ships English only in 0.1 (design 6.5).
    type: 'localization',
    alias: 'UserActivity.Localization.EnGb',
    name: 'User Activity English (UK)',
    meta: { culture: 'en-gb' },
    js: () => import('./localization/en.js'),
  },
  {
    type: 'menuItem',
    alias: 'UserActivity.MenuItem.Activity',
    name: 'User Activity Menu Item',
    // After core's User Groups (100): weight 50.
    weight: 50,
    meta: { label: '#userActivity_activity', icon: 'icon-time', entityType: ROOT_ENTITY_TYPE, menus: ['Umb.Menu.UserManagement'] },
  },
  {
    type: 'workspace',
    kind: 'default',
    alias: ROOT_WORKSPACE_ALIAS,
    name: 'User Activity Root Workspace',
    meta: { entityType: ROOT_ENTITY_TYPE, headline: '#userActivity_activity' },
  },
  {
    type: 'workspaceView',
    alias: 'UserActivity.WorkspaceView.Root.Activity',
    name: 'User Activity Global View',
    element: () => import('./views/global/global-view.element.js'),
    weight: 100,
    meta: { label: '#userActivity_activity', pathname: 'activity', icon: 'icon-time' },
    conditions: [{ alias: 'Umb.Condition.WorkspaceAlias', match: ROOT_WORKSPACE_ALIAS }],
  },
  {
    type: 'modal',
    alias: 'UserActivity.Modal.Detail',
    name: 'User Activity Detail Modal',
    element: () => import('./modal/activity-detail-modal.element.js'),
  },
  {
    type: 'modal',
    alias: MY_ACTIVITY_MODAL_ALIAS,
    name: 'User Activity Your Activity Modal',
    element: () => import('./modal/my-activity-modal.element.js'),
  },
  {
    // The route to your own activity for people without the Users section (QA-F3). Shown only when the API would
    // answer; the API still decides.
    type: 'userProfileApp',
    alias: 'UserActivity.UserProfileApp.Activity',
    name: 'User Activity Profile App',
    // Core's profile apps use 100-1000; a low weight puts this box last, after core's own.
    weight: 10,
    element: () => import('./profile/my-activity-profile-app.element.js'),
    meta: { label: '#userActivity_myActivity', pathname: 'activity' },
  },
  {
    type: 'condition',
    alias: CAN_VIEW_CONDITION,
    name: 'User Activity Can View Condition',
    api: () => import('./conditions/can-view.condition.js'),
  },
  {
    type: 'workspaceView',
    alias: 'UserActivity.WorkspaceView.User.Activity',
    name: 'User Activity Tab',
    element: () => import('./views/user-tab/user-tab.element.js'),
    // Core's Details view is weight 90 (S1), so this tab follows it.
    weight: 50,
    meta: { label: '#userActivity_activity', pathname: 'activity', icon: 'icon-time' },
    conditions: [
      { alias: 'Umb.Condition.WorkspaceAlias', match: USER_WORKSPACE_ALIAS },
      { alias: CAN_VIEW_CONDITION },
    ],
  },
];
