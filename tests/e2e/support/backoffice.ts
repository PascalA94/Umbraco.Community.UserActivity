import type { Page } from '@playwright/test';

export const env = {
  adminEmail: process.env.ADMIN_EMAIL ?? 'admin@example.test',
  adminPassword: required('ADMIN_PASSWORD'),
  testPassword: required('TEST_USER_PASSWORD'),
};

function required(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Set ${name} in tests/e2e/.env (see .env.example).`);
  return value;
}

/** A unique suffix per run, so specs never collide with each other or with earlier runs. */
export const runId = Date.now().toString(36);

export async function login(page: Page, email: string, password: string) {
  await page.goto('/umbraco');
  const user = page.locator('input[name="username"], input[type="email"]').first();
  await user.waitFor();
  await user.fill(email);
  await page.locator('input[type="password"]').first().fill(password);
  await page.keyboard.press('Enter');
  await page.waitForURL(/\/umbraco\/section\//, { timeout: 60_000 });
  // The backoffice keeps booting (manifests, current user) after the redirect; let it settle before API calls.
  await page.waitForLoadState('networkidle');
}

/*
 * Locator rules (learned the hard way on a slow CI runner):
 *  - Never take .first() of a page-wide locator. The backoffice keeps hidden copies of menu items and buttons, and the
 *    sidebar menu is always visible, so on a slow runner a test can read or click the wrong element.
 *  - Scope reads to their container (e.g. `umb-workspace-entity-action-menu uui-menu-item:visible`) and click buttons
 *    with `.filter({ visible: true })`.
 *  - Retry the click that opens a menu or modal until it is really visible (`expect(...).toPass()`): the backoffice
 *    loads an action's code on its first click, and on a slow runner that click can open nothing.
 *  - Keep workers: 1. Umbraco's own user creation deadlocks under concurrent creates, on both databases.
 */
