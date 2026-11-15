import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests against the running stack. Start it first:
 *   docker compose --profile full up -d --build --wait     (from the repo root)
 * then `npm run e2e`. Set E2E_BASE_URL to test another deployment.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
