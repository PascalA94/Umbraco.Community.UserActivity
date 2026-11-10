import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { exportActivityEvents, getActivityEvent, getActivityEvents, getActivityFilterOptions, getUserActivityEvents } from './generated/sdk.gen.js';
import type { ExportActivityEventsData, GetActivityEventsData } from './generated/types.gen.js';

export type * from './generated/types.gen.js';

export type ActivityListQuery = NonNullable<GetActivityEventsData['query']>;
export type ActivityExportQuery = NonNullable<ExportActivityEventsData['query']>;

// Every call goes through core's configured client, so the backoffice's own token handling applies.
const client = umbHttpClient;

export const userActivityApi = {
  filterOptions: (host: UmbControllerHost) => tryExecute(host, getActivityFilterOptions({ client }), { disableNotifications: true }),

  list: (host: UmbControllerHost, query: ActivityListQuery) => tryExecute(host, getActivityEvents({ client, query })),

  listForUser: (host: UmbControllerHost, userKey: string, query: Omit<ActivityListQuery, 'userKey'>) =>
    tryExecute(host, getUserActivityEvents({ client, path: { userKey }, query })),

  /** The CSV as a blob. It goes through the authenticated client, so a plain link would arrive without the token. */
  exportCsv: (host: UmbControllerHost, query: ActivityExportQuery) =>
    tryExecute(host, exportActivityEvents({ client, query, parseAs: 'blob' })),

  detail: (host: UmbControllerHost, source: string, id: string) => tryExecute(host, getActivityEvent({ client, path: { source, id } })),
};
