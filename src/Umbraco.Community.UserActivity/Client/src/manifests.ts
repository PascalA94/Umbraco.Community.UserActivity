import type { ManifestDashboard } from '@umbraco-cms/backoffice/dashboard';

// Placeholder dashboard so the bundle has something to load. Replace or remove it; keep every alias prefixed
// "UserActivity." because aliases are permanent once released.
const dashboard: ManifestDashboard = {
  type: 'dashboard',
  alias: 'UserActivity.Dashboard',
  name: 'User Activity Dashboard',
  element: () => import('./dashboard.element.js'),
  weight: -10,
  meta: {
    label: 'User Activity',
    pathname: 'user-activity',
  },
  conditions: [
    {
      alias: 'Umb.Condition.SectionAlias',
      match: 'Umb.Section.Content',
    },
  ],
};

export const manifests = [dashboard];
