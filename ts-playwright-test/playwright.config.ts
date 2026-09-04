import { defineConfig } from '@playwright/test';
import type { PlaywrightTestOptions } from '@playwright/test';
import { MAX_PARALLEL_WORKERS } from './src/config/parallelSettings';
import { getSettings } from './src/config/settings';
import type { SauceDemoOptions } from './src/fixtures';

const settings = getSettings();
const isCi = process.env.CI === 'true';

/**
 * Headless needs an explicit viewport - there is no real window to size otherwise. Headed mode opts out
 * with `viewport: null` so Chromium's `--start-maximized` governs the size instead, for local debugging.
 * Playwright never resizes a native OS window at all here, it sets the viewport directly, so the
 * headless-virtual-screen trap documented in the Selenium sibling's README cannot occur.
 */
const viewport: PlaywrightTestOptions['viewport'] = settings.headless ? { width: 1920, height: 1080 } : null;

/**
 * Required to launch Chromium at all in most CI containers (no --cap-add=SYS_ADMIN, no /dev/shm sized
 * for a real browser) - the same reason every sibling framework applies these. Firefox and WebKit have
 * no equivalent requirement, hence Chromium-only.
 */
const chromiumArgs = [
  '--no-sandbox',
  '--disable-dev-shm-usage',
  ...(settings.headless ? [] : ['--start-maximized'])
];

export default defineConfig<SauceDemoOptions>({
  testDir: './tests',
  // Only *.spec.ts are browser tests. tests/unit/*.test.ts are plain Node test-runner unit tests with no
  // browser at all, run by `npm run test:unit` - this keeps Playwright from trying to own them.
  testMatch: '**/*.spec.ts',

  // Runs once in the coordinator before any worker starts - Allure environment.properties + categories.json.
  globalSetup: './src/support/environmentWriter.ts',

  fullyParallel: true,
  forbidOnly: isCi,

  // Retries in CI only. Local runs should surface a flake immediately rather than paper over it; CI
  // needs the retry both to survive saucedemo.com's own degradation under automated traffic (see the
  // Selenium sibling's investigation) and, more usefully, because `trace: 'on-first-retry'` below only
  // produces a trace when a retry actually happens.
  retries: isCi ? 2 : 0,

  // Imported, not duplicated: the Cucumber sibling has to repeat this number as a literal in cucumber.js
  // because Cucumber reads that file before any TypeScript loader exists. Playwright transpiles its own
  // config, so there is exactly one copy of this constant in this framework.
  workers: MAX_PARALLEL_WORKERS,

  // Generous per-test budget: performance_glitch_user adds ~5s of artificial delay per navigation, and a
  // full checkout test crosses four pages.
  timeout: 90_000,
  expect: { timeout: settings.explicitWaitMs },

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['json', { outputFile: 'artifacts/playwright-report.json' }],
    ['allure-playwright', { resultsDir: 'allure-results' }]
  ],

  use: {
    baseURL: settings.baseUrl,
    // saucedemo marks its elements with `data-test`, not Playwright's default `data-testid`. Setting this
    // once here is what lets every page object use `getByTestId('username')` instead of repeating a
    // `[data-test='username']` CSS string, as all four sibling frameworks must.
    testIdAttribute: 'data-test',
    headless: settings.headless,
    viewport,
    actionTimeout: settings.explicitWaitMs,
    navigationTimeout: settings.pageLoadTimeoutMs,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ...(settings.remoteUrl ? { connectOptions: { wsEndpoint: settings.remoteUrl } } : {})
  },

  // Explicit `browserName` rather than Playwright's `devices['Desktop Chrome']` preset: that preset
  // pins `channel: 'chrome'`, which requires a vendor-installed Google Chrome on the machine. CI has
  // only Playwright's own bundled engines, so the preset would fail there while working locally on a
  // developer box that happens to have Chrome - exactly the kind of environment-dependent difference
  // the C# Playwright sibling's BrowserType enum documents avoiding.
  projects: [
    { name: 'chromium', use: { browserName: 'chromium', launchOptions: { args: chromiumArgs } } },
    { name: 'firefox', use: { browserName: 'firefox' } },
    { name: 'webkit', use: { browserName: 'webkit' } }
  ]
});
