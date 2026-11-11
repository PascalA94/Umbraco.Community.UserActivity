// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import '../src/components/activity-table.element.js';
import en from '../src/localization/en.js';
import type { ActivityEventResponseModel } from '../src/api/user-activity.api.js';

// The recorder writes these four entity types (design 4.1). Without a label the table shows the raw name.
const RECORDER_ENTITY_TYPES = { cspPolicy: 'CSP policy', workflowConfig: 'Workflow setting', workflowInstance: 'Workflow', redirect: 'Redirect' };

const dictionary = en.userActivity as Record<string, unknown>;
const localize = (key: string, ...args: unknown[]) => {
  const value = dictionary[key.replace(/^userActivity_/, '')];
  return typeof value === 'function' ? (value as (...a: unknown[]) => string)(...args) : typeof value === 'string' ? value : key;
};

describe('recorder entity type labels', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it.each(Object.entries(RECORDER_ENTITY_TYPES))('%s has a label', (type, label) => {
    expect(dictionary[`entityType_${type}`]).toBe(label);
  });

  it('the table shows the label and not the raw entity type', async () => {
    const item = { source: 'recorder', id: 'rec:1', occurredUtc: '2026-11-09T10:00:00Z', actor: { kind: 'user', label: 'Wren Writer' }, category: 'saved', rawType: 'CspSavedNotification', entity: { type: 'cspPolicy', name: 'Front end policy', exists: false } } as ActivityEventResponseModel;
    const table = document.createElement('activity-table') as HTMLElement & { updateComplete: Promise<unknown>; items: unknown; localize: unknown };
    table.items = [item];
    table.localize = localize;
    document.body.appendChild(table);
    await table.updateComplete;
    const small = table.shadowRoot!.querySelector('.item small')!;
    expect(small.textContent).toBe('CSP policy');
  });
});
