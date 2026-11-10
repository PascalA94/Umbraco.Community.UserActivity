import { describe, expect, it } from 'vitest';
import {
  DEFAULT_RANGE_DAYS,
  filtersFromSearch,
  filtersToSearch,
  toApiQuery,
  type ActivityFilters,
} from '../src/state/activity-filter-state.js';

const USER = '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f';

describe('activity filter state', () => {
  it('FilterState_RoundTripsUrl: every filter, page, empty values dropped', () => {
    const full: ActivityFilters = {
      userKey: USER,
      sources: ['core', 'ai'],
      categories: ['published', 'deleted'],
      entityTypes: ['document'],
      search: 'home page',
      from: '2026-10-01',
      to: '2026-10-31',
      page: 3,
    };
    const search = filtersToSearch(full);
    expect(filtersFromSearch(search)).toEqual(full);

    const empty: ActivityFilters = { sources: [], categories: [], entityTypes: [], search: '', page: 1 };
    expect(filtersToSearch(empty)).toBe('');
    expect(filtersToSearch({ ...empty, search: '   ', sources: [''], page: 1 })).toBe('');
    expect(filtersFromSearch('?user=&source=&page=')).toEqual(empty);
  });

  it('FilterState_RoundTripsUrl: a preset range survives', () => {
    const f = filtersFromSearch('?range=7d');
    expect(f.range).toBe('7d');
    expect(filtersToSearch(f)).toBe('range=7d');
  });

  it('FilterState_DefaultIsLast30Days', () => {
    const now = new Date('2026-11-09T12:00:00Z');
    const f = filtersFromSearch('');
    expect(f.range).toBeUndefined();
    expect(DEFAULT_RANGE_DAYS).toBe(30);
    const q = toApiQuery(f, now, 50);
    expect(q.from).toBe('2026-10-10T12:00:00.000Z');
    expect(q.to).toBeUndefined();
    expect(q.skip).toBe('0');
    expect(q.take).toBe('50');
  });

  it('FilterState_UnknownParamsIgnored: junk never reaches the API query', () => {
    const f = filtersFromSearch('?foo=bar&page=abc&range=forever&from=yesterday&user=not-a-guid&source=%3Cb%3E&page=-4');
    expect(f.page).toBe(1);
    expect(f.range).toBeUndefined();
    expect(f.from).toBeUndefined();
    expect(f.userKey).toBeUndefined();
    expect(filtersToSearch(f)).toBe('source=%3Cb%3E');
    expect(Object.keys(toApiQuery(f, new Date(), 50))).not.toContain('foo');
  });

  it('page maps to skip, custom dates to an inclusive UTC window, a short search is not sent', () => {
    const f = filtersFromSearch('?page=3&from=2026-10-01&to=2026-10-31&search=ab');
    const q = toApiQuery(f, new Date('2026-11-09T12:00:00Z'), 50);
    expect(q.skip).toBe('100');
    expect(q.from).toBe('2026-10-01T00:00:00.000Z');
    expect(q.to).toBe('2026-10-31T23:59:59.999Z');
    expect(q.search).toBeUndefined();
    expect(toApiQuery(filtersFromSearch('?search=abc'), new Date(), 50).search).toBe('abc');
  });
});
