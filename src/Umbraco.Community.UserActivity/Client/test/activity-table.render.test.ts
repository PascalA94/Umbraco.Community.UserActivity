// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import '../src/components/activity-table.element.js';
import type { ActivityEventResponseModel } from '../src/api/user-activity.api.js';

const HOSTILE = '<img src=x onerror=window.__xss=1>';
const FORMULA = '=HYPERLINK("http://x")';

const event = (over: Partial<ActivityEventResponseModel> = {}): ActivityEventResponseModel => ({
  source: 'core',
  id: 'audit:1',
  occurredUtc: '2026-11-09T10:00:00Z',
  actor: { kind: 'user', userKey: '1f0c2d3e-4a5b-4c6d-8e7f-0a1b2c3d4e5f', name: HOSTILE, label: HOSTILE }, // gitleaks:allow
  category: 'published',
  rawType: 'Publish',
  entity: { type: 'document', key: null, id: '1', name: HOSTILE, exists: false },
  summary: FORMULA,
  ...over,
});

type Table = HTMLElement & { items: ActivityEventResponseModel[]; busy: boolean; localize: (k: string, ...a: unknown[]) => string; updateComplete: Promise<unknown> };

async function render(items: ActivityEventResponseModel[]) {
  const el = document.createElement('activity-table') as Table;
  el.items = items;
  el.localize = (k) => k;
  document.body.appendChild(el);
  await el.updateComplete;
  return el;
}

describe('activity-table', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('Table_RendersTextOnly: a hostile name is text and creates no element', async () => {
    const el = await render([event()]);
    const root = el.shadowRoot!;
    expect(root.querySelector('img')).toBeNull();
    expect(root.textContent).toContain(HOSTILE);
    expect(root.textContent).toContain(HOSTILE);
    expect((window as unknown as { __xss?: number }).__xss).toBeUndefined();
  });

  it('shows the user column by default and drops it only when hide-user is set', async () => {
    const el = await render([event()]);
    expect(el.shadowRoot!.querySelectorAll('uui-table-head-cell').length).toBe(5);
    (el as unknown as { hideUser: boolean }).hideUser = true;
    await el.updateComplete;
    expect(el.shadowRoot!.querySelectorAll('uui-table-head-cell').length).toBe(4);
  });

  it('a formula-looking item name is text too', async () => {
    const el = await render([event({ entity: { type: 'document', key: null, id: '2', name: FORMULA, exists: false } })]);
    expect(el.shadowRoot!.textContent).toContain(FORMULA);
    expect(el.shadowRoot!.querySelector('.item a')).toBeNull();
  });

  it('opens an event from a real button and tags aria-busy while loading', async () => {
    const el = await render([event()]);
    const seen: string[] = [];
    el.addEventListener('activity-open', (e) => seen.push((e as CustomEvent<{ id: string }>).detail.id));
    el.shadowRoot!.querySelector<HTMLButtonElement>('button.open')!.click();
    expect(seen).toEqual(['audit:1']);
    el.busy = true;
    await el.updateComplete;
    expect(el.shadowRoot!.querySelector('uui-table')!.getAttribute('aria-busy')).toBe('true');
  });

  it('shows the via actor and a time element with a full date title', async () => {
    const via = { kind: 'serviceAccount', userKey: null, name: 'Automate', label: 'Automate' };
    const el = await render([event({ actor: { kind: 'user', userKey: null, name: 'Sam', label: 'Sam', via } })]);
    el.localize = (k, ...a: unknown[]) => (k === 'userActivity_via' ? `via ${a[0]}` : k);
    await el.updateComplete;
    expect(el.shadowRoot!.textContent).toContain('via Automate');
    const time = el.shadowRoot!.querySelector('time')!;
    expect(time.getAttribute('datetime')).toBe('2026-11-09T10:00:00Z');
    expect(time.getAttribute('title')).toBeTruthy();
  });
});
