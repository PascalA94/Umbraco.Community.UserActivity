import { defineConfig } from 'vitest/config';

// Pure modules run in Node; files that render Lit elements opt into happy-dom per file.
// Package elements import Lit through the backoffice's re-export; tests resolve that to Lit itself.
export default defineConfig({
  resolve: {
    alias: [{ find: /^@umbraco-cms\/backoffice\/external\/lit$/, replacement: 'lit' }],
  },
  test: {
    include: ['test/**/*.test.ts'],
    environment: 'node',
  },
});
