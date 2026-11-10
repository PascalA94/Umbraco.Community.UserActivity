import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { demoUsers, login, passwordOf } from '../support/backoffice';
import { consoleGuard } from '../support/console-guard';

const GLOBAL_URL = '/umbraco/section/user-management/workspace/user-activity-root/view/activity';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const HOSTILE = '<img src=x onerror=alert(1)>';

// Scoped, visible-only locators (README CI rule 11): every read starts at the activity view, never the page.
const view = (page: Page) => page.locator('user-activity-view');
const rows = (page: Page) => view(page).locator('activity-table uui-table-row').filter({ hasNot: page.locator('uui-table-head-cell') });
const rowButtons = (page: Page) => view(page).locator('activity-table button.open');

async function openGlobal(page: Page) {
  await page.goto(GLOBAL_URL);
  await expect(rowButtons(page).first()).toBeVisible();
}

async function openFilter(page: Page, popoverId: string) {
  const button = view(page).locator(`activity-filter-bar uui-button[popovertarget="${popoverId}"]`).filter({ visible: true });
  const popover = view(page).locator(`activity-filter-bar #${popoverId}`);
  // The first click can open nothing while the popover's code loads on a slow runner.
  await expect(async () => {
    await button.click();
    await expect(popover.locator('uui-checkbox').first()).toBeVisible({ timeout: 2_000 });
  }).toPass();
  return popover;
}

async function profileLink(page: Page, name: string): Promise<string> {
  const link = view(page).locator('activity-table a', { hasText: name }).filter({ visible: true }).first();
  await expect(link).toBeVisible();
  return (await link.getAttribute('href'))!;
}

test.describe.configure({ mode: 'serial' });

test.beforeAll(async ({ browser }) => {
  // Give Wren some activity of their own (a sign-in row) so their tab has something to show.
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  await login(page, demoUsers.wren.email, passwordOf('wren'));
  await context.close();
});

test('admin filters global view and URL restores', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, demoUsers.sam.email, passwordOf('sam'));
  await openGlobal(page);

  const source = await openFilter(page, 'activity-filter-source');
  await source.locator('uui-checkbox').first().click();
  await expect(page).toHaveURL(/[?&]source=core/);

  await view(page).locator('activity-filter-bar uui-select').evaluate((el) => {
    (el as unknown as { value: string }).value = '7d';
    el.dispatchEvent(new Event('change'));
  });
  await expect(page).toHaveURL(/[?&]range=7d/);

  await page.reload();
  await expect(rowButtons(page).first()).toBeVisible();
  await expect(page).toHaveURL(/source=core/);
  await expect(page).toHaveURL(/range=7d/);
  const restored = await openFilter(page, 'activity-filter-source');
  await expect(restored.locator('uui-checkbox').first()).toHaveJSProperty('checked', true);
  guard.assertNoErrors();
});

test('user tab shows only that user, and admin sees own tab', async ({ page }) => {
  await login(page, demoUsers.sam.email, passwordOf('sam'));
  await openGlobal(page);

  const wrenHref = await profileLink(page, demoUsers.wren.name);
  const wrenKey = /edit\/([0-9a-f-]{36})\//i.exec(wrenHref)![1]!;
  const samKey = /edit\/([0-9a-f-]{36})\//i.exec(await profileLink(page, demoUsers.sam.name))![1]!;

  // Every list request the tab makes goes to Wren's per-user endpoint, even when the URL asks for someone else.
  const listPaths: string[] = [];
  page.on('request', (r) => {
    const path = new URL(r.url()).pathname;
    if (path.includes('/user-activity/') && path.endsWith('/events')) listPaths.push(path);
  });
  await page.goto(`${wrenHref}?user=${samKey}`);
  await expect(view(page).locator('h2')).toContainText(demoUsers.wren.name);
  await expect(rowButtons(page).first()).toBeVisible();
  expect(listPaths.length).toBeGreaterThan(0);
  for (const path of listPaths) expect(path).toContain(`/user-activity/users/${wrenKey}/events`);
  // Locked user: no user column and no user picker on the tab.
  await expect(view(page).locator('activity-table uui-table-head-cell', { hasText: 'User' })).toHaveCount(0);
  await expect(view(page).locator('umb-user-input')).toHaveCount(0);

  // Admin opening their own tab gets data, not a 403 (design 7.2).
  await openGlobal(page);
  await page.goto(await profileLink(page, demoUsers.sam.name));
  await expect(view(page).locator('h2')).toContainText(demoUsers.sam.name);
  await expect(rowButtons(page).first()).toBeVisible();
});

test('detail opens and closes with keyboard, focus returns', async ({ page }) => {
  await login(page, demoUsers.sam.email, passwordOf('sam'));
  await openGlobal(page);
  const button: Locator = rowButtons(page).first();
  await button.focus();
  const modal = page.locator('user-activity-detail-modal');
  await expect(async () => {
    await page.keyboard.press('Enter');
    await expect(modal.locator('activity-detail')).toBeVisible({ timeout: 2_000 });
  }).toPass();
  await page.keyboard.press('Escape');
  await expect(modal).toHaveCount(0);
  await expect(button).toBeFocused();
});

test('hostile names render as text', async ({ page }) => {
  let dialog = false;
  page.on('dialog', async (d) => {
    dialog = true;
    await d.dismiss();
  });
  await login(page, demoUsers.sam.email, passwordOf('sam'));
  await page.goto(`${GLOBAL_URL}?search=onerror`);
  const hit = view(page).locator('activity-table .item').filter({ hasText: HOSTILE }).first();
  await expect(hit).toBeVisible();
  await expect(view(page).locator('activity-table img')).toHaveCount(0);

  // The same name in the sidebar (headline and item section) is text too.
  const modal = page.locator('user-activity-detail-modal');
  await expect(async () => {
    await rowButtons(page).first().click();
    await expect(modal.locator('activity-detail')).toBeVisible({ timeout: 2_000 });
  }).toPass();
  await expect(modal.locator('activity-detail')).toContainText(HOSTILE);
  await expect(modal.locator('img')).toHaveCount(0);
  expect(dialog).toBe(false);
});

test('editor sees no tab and gets no data by URL', async ({ page }) => {
  await login(page, demoUsers.ed.email, passwordOf('ed'));
  await page.goto(GLOBAL_URL);
  await page.waitForLoadState('networkidle');
  await expect(view(page).locator('activity-table')).toHaveCount(0);
  await expect(page.locator('uui-menu-item[label="Activity"]:visible')).toHaveCount(0);
});

test('axe has no violations on both views and the open sidebar', async ({ page }) => {
  await login(page, demoUsers.sam.email, passwordOf('sam'));
  await openGlobal(page);
  // axe's include() can't reach into nested shadow roots, so scan the page and keep only findings inside our elements.
  // The backoffice's own chrome is core's to fix, not ours.
  const scan = async (tag: string) => {
    const result = await new AxeBuilder({ page: page as never }).withTags(AXE_TAGS).analyze();
    // Guard against a vacuous pass: axe must have actually evaluated nodes inside the element.
    expect(result.passes.some((p) => p.nodes.some((n) => JSON.stringify(n.target).includes(tag)))).toBe(true);
    const ours = result.violations
      .map((v) => ({ ...v, nodes: v.nodes.filter((n) => JSON.stringify(n.target).includes(tag)) }))
      .filter((v) => v.nodes.length > 0);
    return { violations: ours.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.map((n) => n.target) })) };
  };

  expect((await scan('user-activity-view')).violations).toEqual([]);

  const wrenHref = await profileLink(page, demoUsers.wren.name);
  await page.goto(wrenHref);
  await expect(rowButtons(page).first()).toBeVisible();
  expect((await scan('user-activity-view')).violations).toEqual([]);

  const modal = page.locator('user-activity-detail-modal');
  await rowButtons(page).first().focus();
  await expect(async () => {
    await page.keyboard.press('Enter');
    await expect(modal.locator('activity-detail')).toBeVisible({ timeout: 2_000 });
  }).toPass();
  expect((await scan('user-activity-detail-modal')).violations).toEqual([]);
});

// Draft screenshots (fake data only) for the README; final ones come in Task 27. Run with SCREENSHOTS=1.
test('draft screenshots', async ({ page }) => {
  test.skip(!process.env.SCREENSHOTS, 'set SCREENSHOTS=1 to write assets/screenshots');
  mkdirSync('../../assets/screenshots', { recursive: true });
  await login(page, demoUsers.sam.email, passwordOf('sam'));
  await openGlobal(page);
  await page.screenshot({ path: '../../assets/screenshots/global-view.png' });
  await page.goto(await profileLink(page, demoUsers.wren.name));
  await expect(rowButtons(page).first()).toBeVisible();
  await page.screenshot({ path: '../../assets/screenshots/user-tab.png' });
});
