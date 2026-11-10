// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import '../src/components/activity-source-tag.element.js';

type Tag = HTMLElement & { alias: string; displayNameKey: string; localize: (key: string) => string; updateComplete: Promise<unknown> };

describe('activity-source-tag', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('SourceTag_UsesLocalizedName', async () => {
    const el = document.createElement('activity-source-tag') as Tag;
    el.alias = 'core';
    el.displayNameKey = 'userActivity_source_core';
    el.localize = (key) => (key === 'userActivity_source_core' ? 'Umbraco' : key);
    document.body.appendChild(el);
    await el.updateComplete;
    const tag = el.shadowRoot!.querySelector('uui-tag')!;
    expect(tag.textContent?.trim()).toBe('Umbraco');
    expect(tag.getAttribute('look')).toBe('secondary');
  });

  it('falls back to the alias when no key resolves', async () => {
    const el = document.createElement('activity-source-tag') as Tag;
    el.alias = 'custom';
    el.displayNameKey = '';
    document.body.appendChild(el);
    await el.updateComplete;
    expect(el.shadowRoot!.querySelector('uui-tag')!.textContent?.trim()).toBe('custom');
  });
});
