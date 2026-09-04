import { defineConfig } from '@playwright/test';

const isCi = process.env.CI === 'true';
const port = Number(process.env.FIXTURES_PORT || 8100);
const baseURL = `http://127.0.0.1:${port}/`;

export default defineConfig({
  testDir: './tests',
  // globalSetup.ts also lives under tests/, and is deliberately not matched here.
  testMatch: '**/*.spec.ts',

  // Allure environment.properties + categories.json, once per run in the coordinator.
  globalSetup: './tests/globalSetup.ts',

  fullyParallel: true,
  forbidOnly: isCi,
  retries: isCi ? 1 : 0,

  // No `workers` cap here, unlike the two saucedemo frameworks. Their limit of 3 exists because
  // saucedemo has exactly three checkout-capable accounts to hand out; this suite talks only to a local
  // fixture app with no accounts and no shared state, so there is nothing to serialize and Playwright's
  // default (half the available cores) is right.
  timeout: 30_000,

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['json', { outputFile: 'artifacts/playwright-report.json' }],
    ['allure-playwright', { resultsDir: 'allure-results' }]
  ],

  // Playwright starts the fixture app itself and waits for it to answer before the first test, then
  // shuts it down at the end. This is the feature that lets the whole suite run offline with no
  // third-party practice site, no Docker, and no "remember to start the server first" step in the
  // README - and it is itself one of the roadmap items being demonstrated.
  webServer: {
    command: 'node fixtures-app/server.mjs',
    url: baseURL,
    // Locally, reuse a server you already have running (npm run fixtures-app) instead of fighting over
    // the port. In CI there is never one to reuse, and reusing a stale process would hide a broken start.
    reuseExistingServer: !isCi,
    timeout: 30_000,
    stdout: 'pipe',
    stderr: 'pipe'
  },

  use: {
    baseURL,
    testIdAttribute: 'data-test',
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },

  // Explicit `browserName` rather than the `devices['Desktop Chrome']` preset, which pins
  // `channel: 'chrome'` and so needs a vendor-installed Chrome. Device emulation is applied per-spec
  // with `test.use({ ...devices['Pixel 7'] })` (see tests/mechanics/mobile.spec.ts) rather than by
  // adding mobile projects here, so the emulation stays visible next to the tests that depend on it.
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium', launchOptions: { args: ['--no-sandbox', '--disable-dev-shm-usage'] } }
    },
    { name: 'firefox', use: { browserName: 'firefox' } },
    { name: 'webkit', use: { browserName: 'webkit' } }
  ]
});
