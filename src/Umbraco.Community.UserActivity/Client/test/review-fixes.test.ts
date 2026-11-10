// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import '../src/components/activity-detail.element.js';
import '../src/components/activity-filter-bar.element.js';
import '../src/components/activity-pager.element.js';
import '../src/components/activity-table.element.js';
import { clearFilters, type ActivityFilters } from '../src/state/activity-filter-state.js';
import { itemHref, actorDisplay } from '../src/components/format.js';
import { makeLocalize } from '../src/components/localize.js';
import type { ActivityEventDetailResponseModel, ActivityEventResponseModel } from '../src/api/user-activity.api.js';

const HOSTILE = '<img src=x onerror=window.__xss=1>';
const base: ActivityFilters = { userKey: '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f', sources: ['core'], categories: ['saved'], entityTypes: ['document'], search: 'abc', range: '7d', page: 4 }; // gitleaks:allow

async function mount(tag: string, props: Record<string, unknown>) {
  const el = document.createElement(tag) as HTMLElement & { updateComplete: Promise<unknown> };
  Object.assign(el, props);
  document.body.appendChild(el);
  await el.updateComplete;
  return el;
}

describe('review fixes', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('QA-2 clearFilters: resets everything, keeps a locked user, drops an unlocked one, back to page 1', () => {
    expect(clearFilters(base, false)).toEqual({ userKey: undefined, sources: [], categories: [], entityTypes: [], search: '', range: undefined, from: undefined, to: undefined, page: 1 });
    expect(clearFilters(base, true).userKey).toBe(base.userKey);
  });

  it('QA-1: the range select labels the server default window, not a hard-coded 30 days', async () => {
    const options = { sources: [], categories: [], entityTypes: [], defaultRangeDays: 7, maxRangeDays: 366, maxTake: 100, canReadAll: true, exportEnabled: false, showIpAddresses: false };
    const bar = await mount('activity-filter-bar', { filters: { sources: [], categories: [], entityTypes: [], search: '', page: 1 }, options });
    const select = bar.shadowRoot!.querySelector('uui-select') as unknown as { options: { value: string; selected: boolean }[] };
    expect(select.options.find((o) => o.selected)?.value).toBe('7d');
    let detail: ActivityFilters | undefined;
    bar.addEventListener('filters-change', (e) => (detail = (e as CustomEvent<ActivityFilters>).detail));
    const el = bar.shadowRoot!.querySelector('uui-select') as HTMLElement & { value: string };
    Object.defineProperty(el, 'value', { value: '7d', configurable: true });
    el.dispatchEvent(new Event('change'));
    expect(detail?.range).toBeUndefined();
  });

  it('QA-4: the user picker has an accessible name; the search is capped at 100 characters', async () => {
    const bar = await mount('activity-filter-bar', { filters: { sources: [], categories: [], entityTypes: [], search: '', page: 1 }, localize: (k: string) => (k === 'userActivity_filterUser' ? 'User' : k) });
    expect(bar.shadowRoot!.querySelector('umb-user-input')!.getAttribute('aria-label')).toBe('User');
    expect(bar.shadowRoot!.querySelector('uui-input.search')!.getAttribute('maxlength')).toBe('100');
  });

  it('QA-3: one persistent live region across empty, loading and results', async () => {
    const pager = (await mount('activity-pager', { count: 0, localize: (k: string) => k })) as HTMLElement & { count: number; loading: boolean; updateComplete: Promise<unknown> };
    const region = pager.shadowRoot!.querySelector('[aria-live="polite"]')!;
    expect(region.textContent).toContain('userActivity_empty');
    pager.loading = true;
    await pager.updateComplete;
    expect(pager.shadowRoot!.querySelector('[aria-live="polite"]')).toBe(region);
    expect(region.textContent).toContain('userActivity_loading');
    pager.loading = false;
    pager.count = 5;
    await pager.updateComplete;
    expect(pager.shadowRoot!.querySelector('[aria-live="polite"]')).toBe(region);
    expect(region.textContent).toContain('userActivity_showingRange');
  });

  it('QA-5: each row button names the time, the action and the item', async () => {
    const item = { source: 'core', id: 'a:1', occurredUtc: '2026-11-09T10:00:00Z', actor: { kind: 'system', label: 'System' }, category: 'saved', rawType: 'Save', entity: { type: 'document', name: 'Home', exists: false } } as ActivityEventResponseModel;
    const table = await mount('activity-table', { items: [item], localize: (k: string, ...a: unknown[]) => (k === 'userActivity_openDetail' ? `OPEN ${a[2]}` : k) });
    expect(table.shadowRoot!.querySelector('button.open')!.getAttribute('aria-label')).toBe('OPEN Home');
  });

  it('QA-8: hostile entity name, actor name and raw type in the detail are text only', async () => {
    const event = {
      source: 'core', id: 'a:1', occurredUtc: '2026-11-09T10:00:00Z', category: 'saved', rawType: HOSTILE, summary: HOSTILE, ipAddress: null,
      actor: { kind: 'user', userKey: null, name: HOSTILE, label: HOSTILE },
      entity: { type: HOSTILE, key: null, id: '1', name: HOSTILE, exists: false },
      details: { [HOSTILE]: HOSTILE },
    } as ActivityEventDetailResponseModel;
    const el = await mount('activity-detail', { event });
    expect(el.shadowRoot!.querySelector('img')).toBeNull();
    expect(el.shadowRoot!.textContent).toContain(HOSTILE);
  });

  it('QA-10: prototype-ish names from a provider cannot break lookups', async () => {
    expect(itemHref({ type: '__proto__', key: '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f', exists: true } as never)).toBeUndefined(); // gitleaks:allow
    expect(itemHref({ type: 'constructor', key: '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f', exists: true } as never)).toBeUndefined(); // gitleaks:allow
    expect(actorDisplay({ kind: 'constructor', label: 'Fallback' } as never, (k) => k)).toBe('Fallback');
    const item = { source: '__proto__', id: 'a:1', occurredUtc: '2026-11-09T10:00:00Z', actor: { kind: '__proto__', label: 'X' }, category: 'saved', rawType: 'Save', entity: { type: '__proto__', name: 'n', exists: true, key: '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f' } } as ActivityEventResponseModel; // gitleaks:allow
    const table = await mount('activity-table', { items: [item] });
    expect(table.shadowRoot!.querySelectorAll('uui-table-row').length).toBeGreaterThan(0);
  });

  it("QA-11: dynamic keys reach the backoffice only when they are this package's own", () => {
    const seen: string[] = [];
    const localize = makeLocalize({ term: ((key: string) => (seen.push(key), `T:${key}`)) as never });
    expect(localize('userActivity_sourceCore')).toBe('T:userActivity_sourceCore');
    expect(localize('someone_else_key')).toBe('someone_else_key');
    expect(seen).toEqual(['userActivity_sourceCore']);
  });
});
