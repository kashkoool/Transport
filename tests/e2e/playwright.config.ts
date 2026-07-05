import { defineConfig, devices } from '@playwright/test';

// E2E runs against the full local compose stack (web on :8080 proxies /api to the API).
//   cd transport-platform && POSTGRES_PASSWORD=... docker compose up -d
//   cd tests/e2e && npx playwright test
export default defineConfig({
  testDir: '.',
  globalSetup: './global-setup.ts',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
