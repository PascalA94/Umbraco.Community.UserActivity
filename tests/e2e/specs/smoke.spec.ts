import { expect, test } from '@playwright/test';
import { env, login } from '../support/backoffice';
import { consoleGuard } from '../support/console-guard';

test('the admin can sign in and the backoffice loads the package bundle without console errors', async ({ page }) => {
  const guard = consoleGuard(page);
  await login(page, env.adminEmail, env.adminPassword);
  await expect(page).toHaveURL(/\/umbraco\/section\//);
  guard.assertNoErrors();
});
