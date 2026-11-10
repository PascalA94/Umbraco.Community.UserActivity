// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import { manifests } from '../src/manifests.js';
import '../src/components/activity-filter-bar.element.js';
import '../src/components/activity-pager.element.js';
import type { ActivityFilterOptionsResponseModel } from '../src/api/user-activity.api.js';
import type { ActivityFilters } from '../src/state/activity-filter-state.js';

const OPTIONS: ActivityFilterOptionsResponseModel = {
  sources: [
    { alias: 'core', displayNameKey: 'userActivity_sourceCore', canCount: true, canSearch: true, retentionHintDays: null },
    { alias: 'ai', displayNameKey: 'userActivity_sourceAi', canCount: true, canSearch: false, retentionHintDays: 14 },
  ],
  categories: ['published', 'deleted'],
  entityTypes: ['document', 'media'],
  defaultRangeDays: 30,
  maxRangeDays: 366,
  maxTake: 100,
  canReadAll: true,
  exportEnabled: false,
  showIpAddresses: false,
};
const FILTERS: ActivityFilters = { sources: [], categories: [], entityTypes: [], search: '', page: 1 };

type Bar = HTMLElement & { filters: ActivityFilters; options?: ActivityFilterOptionsResponseModel; lockedUser: boolean; localize: (k: string, ...a: unknown[]) => string; updateComplete: Promise<unknown> };

async function bar(over: Partial<Bar> = {}) {
  const el = document.createElement('activity-filter-bar') as Bar;
  el.filters = FILTERS;
  el.options = OPTIONS;
  Object.assign(el, over);
  document.body.appendChild(el);
  await el.updateComplete;
  return el;
}

describe('global view', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('Manifest_MenuItemInUsersSection (S1 aliases)', () => {
    const menu = manifests.find((m) => m.type === 'menuItem') as unknown as { meta: { menus: string[]; entityType: string; icon: string } };
    expect(menu.meta.menus).toEqual(['Umb.Menu.UserManagement']);
    expect(menu.meta.icon).toBe('icon-time');
    const workspace = manifests.find((m) => m.type === 'workspace') as unknown as { meta: { entityType: string } };
    expect(workspace.meta.entityType).toBe(menu.meta.entityType);
    const view = manifests.find((m) => m.type === 'workspaceView' && (m as { alias: string }).alias.includes('Root')) as unknown as {
      conditions: { alias: string; match: string }[];
    };
    expect(view.conditions[0]).toEqual({ alias: 'Umb.Condition.WorkspaceAlias', match: (workspace as unknown as { alias: string }).alias });
    for (const m of manifests) expect((m as { alias: string }).alias.startsWith('UserActivity.')).toBe(true);
  });

  it('GlobalView_AllFiltersPresent', async () => {
    const el = await bar();
    const root = el.shadowRoot!;
    expect(root.querySelector('uui-input.search')).not.toBeNull();
    expect(root.querySelector('umb-user-input')).not.toBeNull();
    expect(root.querySelectorAll('uui-button[popovertarget]').length).toBe(3);
    expect(root.querySelector('uui-select')).not.toBeNull();
    expect(root.querySelector('uui-button[look="secondary"]')).toBeNull();
  });

  it('shows Clear filters only when a filter is set, and the locked tab has no user picker', async () => {
    const el = await bar({ filters: { ...FILTERS, sources: ['core'] }, lockedUser: true });
    expect(el.shadowRoot!.querySelector('uui-button[look="secondary"]')).not.toBeNull();
    expect(el.shadowRoot!.querySelector('umb-user-input')).toBeNull();
  });

  it('SourceFilter_ListsOnlyActiveSources', async () => {
    const el = await bar({ localize: (k) => ({ userActivity_sourceCore: 'Umbraco', userActivity_sourceAi: 'Umbraco.AI' })[k] ?? k });
    const boxes = [...el.shadowRoot!.querySelectorAll('#activity-filter-source uui-checkbox')];
    expect(boxes.map((b) => b.textContent?.trim())).toEqual(['Umbraco', 'Umbraco.AI']);
  });

  it('a changed filter leaves as one event with the page reset', async () => {
    const el = await bar({ filters: { ...FILTERS, page: 4 } });
    let detail: ActivityFilters | undefined;
    el.addEventListener('filters-change', (e) => (detail = (e as CustomEvent<ActivityFilters>).detail));
    const box = el.shadowRoot!.querySelector<HTMLInputElement>('#activity-filter-source uui-checkbox')!;
    (box as unknown as { checked: boolean }).checked = true;
    box.dispatchEvent(new Event('change'));
    expect(detail?.sources).toEqual(['core']);
    expect(detail?.page).toBe(1);
  });

  it('Pagination_AnnouncesRange: live region text', async () => {
    const pager = document.createElement('activity-pager') as HTMLElement & {
      page: number; take: number; count: number; total: number | null; approximate: boolean; locale: string; localize: (k: string, ...a: unknown[]) => string; updateComplete: Promise<unknown>;
    };
    pager.page = 2; pager.take = 50; pager.count = 50; pager.total = 1234; pager.locale = 'en-GB';
    pager.localize = (k, ...a) => (k === 'userActivity_showingRange' ? `Showing ${a[0]} to ${a[1]} of ${a[2]} events` : k);
    document.body.appendChild(pager);
    await pager.updateComplete;
    const live = pager.shadowRoot!.querySelector('[aria-live="polite"]')!;
    expect(live.textContent?.trim()).toBe('Showing 51 to 100 of 1,234 events');
    expect(pager.shadowRoot!.querySelector('uui-pagination')).not.toBeNull();
  });

  it('a lower-bound total says "at least"; booleans default so an absent attribute is the common case', async () => {
    const pager = document.createElement('activity-pager') as HTMLElement & { page: number; count: number; total: number | null; approximate: boolean; localize: (k: string, ...a: unknown[]) => string; updateComplete: Promise<unknown> };
    pager.count = 10; pager.total = 1000; pager.approximate = true;
    pager.localize = (k, ...a) => `${k}:${a.join('|')}`;
    document.body.appendChild(pager);
    await pager.updateComplete;
    expect(pager.shadowRoot!.querySelector('[aria-live="polite"]')!.textContent).toContain('showingRangeAtLeast');
  });
});
