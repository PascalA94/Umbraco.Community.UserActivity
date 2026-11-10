import { describe, expect, it, vi } from 'vitest';
import { manifests } from '../src/manifests.js';
import { buildRequest } from '../src/state/activity-request.js';
import { filtersFromSearch } from '../src/state/activity-filter-state.js';
import { accessFromResult, createAccessCache, tabVisible } from '../src/conditions/can-view.js';

const SAM = '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f';
const WREN = '9a7e1b2c-3d4e-4f50-8a6b-7c8d9e0f1a2b';
const options = (canReadAll: boolean) => ({ data: { canReadAll } as never });

describe('per-user tab', () => {
  it('Manifest_WorkspaceViewOnUserWorkspace (S1 alias)', () => {
    const view = manifests.find((m) => (m as { alias: string }).alias === 'UserActivity.WorkspaceView.User.Activity') as unknown as {
      type: string;
      meta: { pathname: string; icon: string };
      conditions: { alias: string; match?: string }[];
      weight: number;
    };
    expect(view.type).toBe('workspaceView');
    expect(view.meta.pathname).toBe('activity');
    expect(view.conditions).toContainEqual({ alias: 'Umb.Condition.WorkspaceAlias', match: 'Umb.Workspace.User' });
    expect(view.conditions.some((c) => c.alias === 'UserActivity.Condition.CanView')).toBe(true);
    // Core's Details view has weight 90 in the user workspace, so ours sits after it.
    expect(view.weight).toBeLessThan(90);
    expect(manifests.some((m) => m.type === 'condition' && (m as { alias: string }).alias === 'UserActivity.Condition.CanView')).toBe(true);
  });

  it('UserTab_UserFilterLocked: the request always carries the workspace user, whatever the URL says', () => {
    const filters = filtersFromSearch(`?user=${WREN}&source=core`);
    const request = buildRequest(filters, SAM, new Date('2026-11-09T12:00:00Z'), 50);
    expect(request.kind).toBe('user');
    if (request.kind === 'user') {
      // The URL asked for WREN; the lock (the workspace's key) wins.
      expect(filters.userKey).toBe(WREN);
      expect(request.userKey).toBe(SAM);
      expect('userKey' in request.query).toBe(false);
      expect(request.query.source).toEqual(['core']);
    }
    const global = buildRequest(filters, undefined, new Date(), 50);
    expect(global.kind).toBe('global');
    if (global.kind === 'global') expect(global.query.userKey).toBe(WREN);
  });

  it('Condition_AdminSeesAnyTabIncludingOwn', () => {
    const access = accessFromResult(options(true));
    expect(tabVisible(access, WREN, SAM)).toBe(true);
    expect(tabVisible(access, SAM, SAM)).toBe(true);
  });

  it('Condition_NonAdminOwnTabOnlyWithSelfView', () => {
    const self = accessFromResult(options(false));
    expect(tabVisible(self, SAM, SAM)).toBe(true);
    expect(tabVisible(self, WREN, SAM)).toBe(false);
    expect(tabVisible(self, undefined, SAM)).toBe(false);
  });

  it('Condition_HiddenWhenFilterOptions403', () => {
    const access = accessFromResult({ error: { status: 403 } });
    expect(tabVisible(access, SAM, SAM)).toBe(false);
    expect(tabVisible(accessFromResult({ error: { status: 401 } }), SAM, SAM)).toBe(false);
  });

  it('Condition_CachedPerSession: one request for many tabs; a failed request is retried', async () => {
    const fetcher = vi.fn().mockResolvedValue(options(true));
    const cache = createAccessCache(fetcher);
    await Promise.all([cache.get(), cache.get(), cache.get()]);
    await cache.get();
    expect(fetcher).toHaveBeenCalledTimes(1);

    const flaky = vi.fn().mockResolvedValueOnce({ error: { status: 503 } }).mockResolvedValueOnce(options(true));
    const retry = createAccessCache(flaky);
    expect((await retry.get()).kind).toBe('none');
    expect((await retry.get()).kind).toBe('reader');
    expect(flaky).toHaveBeenCalledTimes(2);

    const denied = vi.fn().mockResolvedValue({ error: { status: 403 } });
    const forbidden = createAccessCache(denied);
    await forbidden.get();
    await forbidden.get();
    expect(denied).toHaveBeenCalledTimes(1);
  });
});
