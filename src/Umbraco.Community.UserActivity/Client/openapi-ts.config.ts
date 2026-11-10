import { defineConfig } from '@hey-api/openapi-ts';

// Generates the typed client from the package's own OpenAPI document. Run the demo site first
// (dotnet run --project demo/Umbraco.Community.UserActivity.DemoSite), then `npm run generate:api`.
export default defineConfig({
  input: process.env.USER_ACTIVITY_OPENAPI ?? 'https://localhost:44331/umbraco/openapi/user-activity.json',
  output: { path: 'src/api/generated', format: false },
  plugins: ['@hey-api/client-fetch', '@hey-api/sdk', '@hey-api/typescript'],
});
