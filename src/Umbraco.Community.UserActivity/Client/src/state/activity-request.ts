import type { ActivityListQuery } from '../api/user-activity.api.js';
import { toApiQuery, type ActivityFilters } from './activity-filter-state.js';

export type ActivityRequest =
  | { kind: 'global'; query: ActivityListQuery }
  | { kind: 'user'; userKey: string; query: Omit<ActivityListQuery, 'userKey'> };

/**
 * One place decides which endpoint a view calls. With a locked user (the per-user tab) the key comes from the
 * workspace, never from the URL, so editing `?user=` cannot point the tab at someone else. The API still decides
 * access (design 7.2); this only keeps the UI honest.
 */
export function buildRequest(filters: ActivityFilters, lockedUserKey: string | undefined, now: Date, take: number, defaultRangeDays?: number): ActivityRequest {
  const query = toApiQuery(filters, now, take, defaultRangeDays);
  if (!lockedUserKey) return { kind: 'global', query };
  const { userKey: _fromUrl, ...rest } = query;
  return { kind: 'user', userKey: lockedUserKey, query: rest };
}
