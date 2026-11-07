import { expect, type Page } from '@playwright/test';

/**
 * Collects console errors and page errors. Every spec ends with assertNoErrors().
 * The backoffice's silent token refresh before login logs "Token request failed: 400" from core; that one is
 * expected before sign-in and ignored, nothing else is.
 */
export function consoleGuard(page: Page) {
  const errors: string[] = [];
  page.on('console', (m) => {
    if (m.type() !== 'error') return;
    const text = m.text();
    // Before sign-in completes the page is not yet in a backoffice section.
    if (/Token request failed: 400|status of 400 \(\)/.test(text) && !page.url().includes('/umbraco/section/')) return;
    errors.push(text);
  });
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  return {
    errors,
    assertNoErrors() {
      expect(errors, errors.join('\n')).toEqual([]);
    },
  };
}
