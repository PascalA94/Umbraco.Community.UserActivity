import type { ActivityListQuery } from '../api/user-activity.api.js';

/** Design 6.1: views open on the last 30 days. The server's own default is the same, kept in step by the filter options. */
export const DEFAULT_RANGE_DAYS = 30;

export const RANGE_PRESETS = { '24h': 1, '7d': 7, '30d': 30, '90d': 90 } as const;
export type RangePreset = keyof typeof RANGE_PRESETS;

export interface ActivityFilters {
  /** Global view only; the per-user tab locks the user instead. */
  userKey?: string;
  sources: string[];
  categories: string[];
  entityTypes: string[];
  search: string;
  /** A preset, or undefined for the default window. Custom `from`/`to` take precedence over it. */
  range?: RangePreset;
  /** Local calendar dates as yyyy-mm-dd, read as UTC days. */
  from?: string;
  to?: string;
  /** 1-based. */
  page: number;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MIN_SEARCH = 3;
const MAX_SEARCH = 100;

const isPreset = (value: string): value is RangePreset => Object.prototype.hasOwnProperty.call(RANGE_PRESETS, value);

function validDate(value: string | null): string | undefined {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return undefined;
  const parsed = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== value ? undefined : value;
}

const many = (params: URLSearchParams, name: string) => params.getAll(name).map((v) => v.trim()).filter((v) => v.length > 0 && v.length <= 64);

/** Reads filters from a query string; anything unknown or malformed is dropped, never forwarded to the API. */
export function filtersFromSearch(search: string): ActivityFilters {
  const params = new URLSearchParams(search);
  const user = params.get('user')?.trim() ?? '';
  const range = params.get('range') ?? '';
  const page = Number.parseInt(params.get('page') ?? '', 10);
  return {
    userKey: GUID.test(user) ? user : undefined,
    sources: many(params, 'source'),
    categories: many(params, 'category'),
    entityTypes: many(params, 'entityType'),
    search: (params.get('search') ?? '').trim().slice(0, MAX_SEARCH),
    range: isPreset(range) ? range : undefined,
    from: validDate(params.get('from')),
    to: validDate(params.get('to')),
    page: Number.isInteger(page) && page > 1 ? page : 1,
  };
}

/** The query string (no leading `?`) for a set of filters; empty values and defaults are left out. */
export function filtersToSearch(filters: ActivityFilters): string {
  const params = new URLSearchParams();
  if (filters.userKey) params.set('user', filters.userKey);
  for (const v of filters.sources) if (v) params.append('source', v);
  for (const v of filters.categories) if (v) params.append('category', v);
  for (const v of filters.entityTypes) if (v) params.append('entityType', v);
  const text = filters.search.trim();
  if (text) params.set('search', text);
  if (filters.range) params.set('range', filters.range);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  if (filters.page > 1) params.set('page', String(filters.page));
  return params.toString();
}

/** True when anything beyond the default view is set (drives "Clear filters"). Paging does not count. */
export function hasActiveFilters(filters: ActivityFilters, lockedUser = false): boolean {
  return (
    (!lockedUser && !!filters.userKey) ||
    filters.sources.length + filters.categories.length + filters.entityTypes.length > 0 ||
    filters.search.trim().length > 0 ||
    !!filters.range ||
    !!filters.from ||
    !!filters.to
  );
}

/** Everything a "Clear filters" button resets. A locked user (the per-user tab) stays; paging resets. */
export function clearFilters(filters: ActivityFilters, lockedUser: boolean): ActivityFilters {
  return { userKey: lockedUser ? filters.userKey : undefined, sources: [], categories: [], entityTypes: [], search: '', range: undefined, from: undefined, to: undefined, page: 1 };
}

/** Maps filters to the API's query. The user key is added by the caller (global) or travels in the path (tab). */
export function toApiQuery(filters: ActivityFilters, now: Date, take: number, defaultRangeDays = DEFAULT_RANGE_DAYS): ActivityListQuery {
  const query: ActivityListQuery = { order: 'desc', skip: String((Math.max(filters.page, 1) - 1) * take), take: String(take) };
  if (filters.userKey) query.userKey = filters.userKey;
  if (filters.sources.length) query.source = filters.sources;
  if (filters.categories.length) query.category = filters.categories;
  if (filters.entityTypes.length) query.entityType = filters.entityTypes;
  // The API rejects searches under 3 characters, so a half-typed term waits instead of showing an error.
  const text = filters.search.trim();
  if (text.length >= MIN_SEARCH) query.search = text.slice(0, MAX_SEARCH);

  if (filters.from || filters.to) {
    if (filters.from) query.from = `${filters.from}T00:00:00.000Z`;
    if (filters.to) query.to = `${filters.to}T23:59:59.999Z`;
  } else {
    const days = filters.range ? RANGE_PRESETS[filters.range] : defaultRangeDays;
    query.from = new Date(now.getTime() - days * 86_400_000).toISOString();
  }
  return query;
}
