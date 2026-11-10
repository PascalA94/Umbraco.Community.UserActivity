import { describe, expect, it } from 'vitest';
import { manifests } from '../src/manifests.js';
import { accessFromResult, profileVisible } from '../src/conditions/can-view.js';
import en from '../src/localization/en.js';

const options = (canReadAll: boolean) => ({ data: { canReadAll } as never });
const byAlias = (alias: string) => manifests.find((m) => (m as { alias: string }).alias === alias) as unknown as Record<string, any> | undefined;

// A user without the Users section can't open any user workspace, so the user's own activity is also reachable from
// their profile (Pascal, 2026-11-10, final review QA-F3).
describe('profile route to your own activity', () => {
  it('Manifest_UserProfileApp_OpensOwnActivity', () => {
    const app = byAlias('UserActivity.UserProfileApp.Activity');
    expect(app?.type).toBe('userProfileApp');
    expect(app?.meta.label).toBe('#userActivity_myActivity');
    expect(app?.meta.pathname).toBe('activity');
    expect(typeof app?.element).toBe('function');

    const modal = byAlias('UserActivity.Modal.MyActivity');
    expect(modal?.type).toBe('modal');
    expect(typeof modal?.element).toBe('function');
  });

  it('ProfileApp_ShownToReadersAndSelfViewUsers_HiddenOtherwise', () => {
    expect(profileVisible(accessFromResult(options(true)))).toBe(true);
    expect(profileVisible(accessFromResult(options(false)))).toBe(true);
    expect(profileVisible(accessFromResult({ error: { status: 403 } }))).toBe(false);
    expect(profileVisible(accessFromResult({ error: { status: 503 } }))).toBe(false);
  });

  it('Localization_HasTheProfileStrings', () => {
    const terms = (en as unknown as { userActivity: Record<string, unknown> }).userActivity;
    for (const key of ['myActivity', 'openMyActivity', 'myActivityHint']) {
      expect(typeof terms[key]).toBe('string');
    }
  });
});
