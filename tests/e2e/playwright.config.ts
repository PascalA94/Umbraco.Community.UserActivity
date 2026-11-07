import { defineConfig, devices } from '@playwright/test';

// Node 24 reads .env natively; values from the CI environment win.
try {
  process.loadEnvFile('.env');
} catch {
  // no .env: CI passes the variables directly
}

export default defineConfig({
  testDir: './specs',
  // Specs change shared site state (users, content), so they run one at a time.
  workers: 1,
  fullyParallel: false,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  // No retries: a flaky security spec (session, xss) must fail, not pass on a second try.
  retries: 0,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.URL ?? 'https://localhost:44331',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
    viewport: { width: 1400, height: 900 },
  },
});
