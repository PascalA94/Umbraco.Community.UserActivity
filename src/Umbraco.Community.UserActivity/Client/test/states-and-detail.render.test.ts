// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import '../src/components/activity-detail.element.js';
import '../src/components/activity-states.element.js';
import '../src/components/activity-table.element.js';
import '../src/components/activity-filter-bar.element.js';
import type { ActivityEventDetailResponseModel, ActivityFilterOptionsResponseModel, ActivityEventResponseModel } from '../src/api/user-activity.api.js';
import type { ActivityFilters } from '../src/state/activity-filter-state.js';

const localize = (k: string, ...a: unknown[]) =>
  ({
    userActivity_via: `via ${a[0]}`,
    userActivity_retentionHint: `${a[0]} keeps about ${a[1]} days.`,
    userActivity_retentionKeptForever: `${a[0]}: kept forever (no limit set).`,
    userActivity_sourceRecorder: 'Recorded changes',
    userActivity_partialWarning: `Some sources didn't respond: ${a[0]}. Results may be incomplete.`,
    userActivity_detailDefaultAdminNote: 'The default administrator is the built-in account.',
    userActivity_sourceAi: 'Umbraco.AI',
    userActivity_sourceCore: 'Umbraco',
    userActivity_sourceTimedOut: 'timed out',
    userActivity_emptyFiltersNone: 'No activity in the selected period.',
    userActivity_depthCapped: 'Narrow the date range to see older events.',
  })[k] ?? k;

const detail = (over: Partial<ActivityEventDetailResponseModel> = {}): ActivityEventDetailResponseModel => ({
  source: 'core',
  id: 'audit:7',
  occurredUtc: '2026-11-09T10:00:00Z',
  actor: { kind: 'defaultAdmin', userKey: null, name: null, label: 'Default administrator' },
  category: 'signedIn',
  rawType: 'umbraco/user/sign-in/login',
  entity: null,
  summary: null,
  ipAddress: null,
  details: { note: '<b>hello</b>', payload: '{"a":1,"b":[2]}' },
  ...over,
});

async function mount<T extends HTMLElement>(tag: string, props: Record<string, unknown>) {
  const el = document.createElement(tag) as T & { updateComplete: Promise<unknown> };
  Object.assign(el, props);
  document.body.appendChild(el);
  await el.updateComplete;
  return el;
}

describe('detail', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('Detail_ShowsViaAndDefaultAdminNote', async () => {
    const via = { kind: 'serviceAccount', userKey: null, name: 'Automate', label: 'Automate' };
    const el = await mount('activity-detail', { event: detail({ actor: { kind: 'defaultAdmin', userKey: null, name: null, label: 'Default administrator', via } }), localize });
    const text = el.shadowRoot!.textContent!;
    expect(el.shadowRoot!.querySelector('[data-row="via"]')?.textContent).toContain('Automate');
    expect(text).toContain('The default administrator is the built-in account.');
  });

  it('Detail_NoIpRowWhenNull', async () => {
    const without = await mount('activity-detail', { event: detail(), localize });
    expect(without.shadowRoot!.querySelector('[data-row="ip"]')).toBeNull();
    document.body.innerHTML = '';
    const withIp = await mount('activity-detail', { event: detail({ ipAddress: '203.0.113.7' }), localize });
    expect(withIp.shadowRoot!.querySelector('[data-row="ip"]')?.textContent).toContain('203.0.113.7');
  });

  it('Detail_JsonAsPreText: JSON in a pre, markup as text, no element created', async () => {
    const el = await mount('activity-detail', { event: detail(), localize });
    const root = el.shadowRoot!;
    expect(root.querySelector('b')).toBeNull();
    expect(root.textContent).toContain('<b>hello</b>');
    const pre = root.querySelector('pre')!;
    expect(JSON.parse(pre.textContent!)).toEqual({ a: 1, b: [2] });
  });

  it('hides the details section when there are none (self-view users get no details)', async () => {
    const el = await mount('activity-detail', { event: detail({ details: null }), localize });
    expect(el.shadowRoot!.querySelector('[data-section="details"]')).toBeNull();
  });
});

describe('states', () => {
  afterEach(() => (document.body.innerHTML = ''));

  const sources = [
    { alias: 'core', displayNameKey: 'userActivity_sourceCore', canCount: true, canSearch: true, retentionHintDays: null, retentionKeptForever: false },
    { alias: 'ai', displayNameKey: 'userActivity_sourceAi', canCount: true, canSearch: false, retentionHintDays: 14, retentionKeptForever: false },
    { alias: 'recorder', displayNameKey: 'userActivity_sourceRecorder', canCount: true, canSearch: true, retentionHintDays: null, retentionKeptForever: true },
  ];

  it('Empty_ShowsRetentionHints', async () => {
    const el = await mount('activity-states', { empty: true, sources, selectedSources: ['ai'], localize });
    const text = el.shadowRoot!.textContent!;
    expect(text).toContain('Umbraco.AI keeps about 14 days.');
    expect(text).toContain('No activity found.');
    expect(el.shadowRoot!.querySelector('[data-state="empty"]')).not.toBeNull();
  });

  it('Empty_RecorderWithNoLimit_SaysKeptForever', async () => {
    const el = await mount('activity-states', { empty: true, sources, selectedSources: ['recorder'], localize });
    expect(el.shadowRoot!.textContent).toContain('Recorded changes: kept forever (no limit set).');
  });

  it('partial warning shows', async () => {
    const el = await mount('activity-states', { sources, status: [{ alias: 'core', status: 'ok', count: 3 }, { alias: 'ai', status: 'timedOut', count: null }], localize });
    const box = el.shadowRoot!.querySelector('[data-state="partial"]')!;
    expect(box.textContent).toContain("Some sources didn't respond: Umbraco.AI (timed out). Results may be incomplete.");
  });

  it('shows the depth-cap hint', async () => {
    const el = await mount('activity-states', { sources, depthCapped: true, localize });
    expect(el.shadowRoot!.querySelector('[data-state="depth"]')?.textContent).toContain('Narrow the date range');
  });

  it('Loading_SetsAriaBusy', async () => {
    const el = await mount('activity-states', { sources, loading: true, localize });
    expect(el.shadowRoot!.querySelector('uui-loader-bar')).not.toBeNull();
    expect(el.shadowRoot!.querySelector('[aria-busy="true"]')).not.toBeNull();
    (el as unknown as { loading: boolean }).loading = false;
    await (el as unknown as { updateComplete: Promise<unknown> }).updateComplete;
    expect(el.shadowRoot!.querySelector('uui-loader-bar')).toBeNull();
  });
});

describe('table focus and export', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('Detail_FocusReturnsToRow', async () => {
    const item = { source: 'core', id: 'audit:1', occurredUtc: '2026-11-09T10:00:00Z', actor: { kind: 'system', label: 'System' }, category: 'saved', rawType: 'Save' } as ActivityEventResponseModel;
    const table = await mount<HTMLElement & { focusRow(source: string, id: string): void }>('activity-table', { items: [item, { ...item, id: 'audit:2' }] });
    table.focusRow('core', 'audit:2');
    const active = table.shadowRoot!.activeElement as HTMLElement;
    expect(active?.getAttribute('data-id')).toBe('audit:2');
  });

  const options = (exportEnabled: boolean): ActivityFilterOptionsResponseModel => ({
    sources: [], categories: [], entityTypes: [], defaultRangeDays: 30, maxRangeDays: 366, maxTake: 100, canReadAll: true, exportEnabled, showIpAddresses: false,
  });
  const filters: ActivityFilters = { sources: [], categories: [], entityTypes: [], search: '', page: 1 };

  it('Export_ButtonOnlyWhenEnabled', async () => {
    const off = await mount('activity-filter-bar', { filters, options: options(false), localize });
    expect(off.shadowRoot!.querySelector('[data-action="export"]')).toBeNull();
    document.body.innerHTML = '';
    const on = await mount('activity-filter-bar', { filters, options: options(true), localize });
    const button = on.shadowRoot!.querySelector<HTMLElement>('[data-action="export"]')!;
    let fired = false;
    on.addEventListener('export-csv', () => (fired = true));
    button.click();
    expect(fired).toBe(true);
  });
});
