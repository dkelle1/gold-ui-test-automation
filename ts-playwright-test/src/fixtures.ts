import { test as base } from '@playwright/test';
import * as allure from 'allure-js-commons';
import { LoginPage } from './pages/loginPage';
import { InventoryPage } from './pages/inventoryPage';
import { TestLogger } from './support/testLogger';
import { getAssignedUser } from './users/assignedUser';
import { userByUsername } from './users/userCatalog';
import type { UserAccount } from './users/userAccount';

/**
 * Test-scoped *options* - values a test can override with `test.use({ ... })`, as opposed to fixtures
 * it merely consumes.
 */
export interface SauceDemoOptions {
  /**
   * A username from `userCatalog` to use instead of this worker's assigned account, for tests that must
   * target one specific (usually deliberately broken) saucedemo user.
   *
   * The Playwright Test equivalent of the Cucumber sibling's `@user:<username>` tag, and a strictly
   * better one: the tag had to be parsed out of the pickle by hand in `tagHelpers.ts`, whereas this is
   * type-checked, discoverable from the fixture definition, and scoped by ordinary `test.describe`
   * nesting rather than by string matching.
   */
  userOverride: string | undefined;
}

export interface SauceDemoFixtures {
  /** Auto fixture: every test starts on the app's entry page, matching the sibling frameworks' per-scenario navigation. */
  appReady: void;
  /** Auto fixture: browser console output, attached to the report only when the test fails. */
  consoleLog: string[];
  /** This test's structured logger, correlated by test id and attached to the report on failure. */
  log: TestLogger;
  /** The account this test actually runs as - `userOverride` if set, otherwise the worker's assigned account. */
  activeUser: UserAccount;
  loginPage: LoginPage;
  inventoryPage: InventoryPage;
  /** Precondition bundle for cart/checkout tests: logged in, product grid rendered, cart empty. The `Background:` of the sibling's Cart/Checkout features. */
  loggedIn: InventoryPage;
}

export interface SauceDemoWorkerFixtures {
  /** One saucedemo account per worker, fixed for that worker's whole lifetime. */
  assignedUser: UserAccount;
}

export const test = base.extend<SauceDemoOptions & SauceDemoFixtures, SauceDemoWorkerFixtures>({
  userOverride: [undefined, { option: true }],

  // Worker-scoped: resolved once per worker process, not once per test. `parallelIndex` is Playwright's
  // own guarantee of a stable integer in [0, workers) for this worker's entire life - no env var to set,
  // no lease to acquire and release. Compare the two C# siblings, which need a real BlockingCollection
  // pool because their runtimes cannot pin an account to a worker this way.
  assignedUser: [
    // Playwright works out a fixture's dependencies by statically parsing this destructuring pattern, so
    // the empty object literal is load-bearing syntax and cannot be simplified to `_`.
    // eslint-disable-next-line no-empty-pattern
    async ({}, use, workerInfo) => {
      await use(getAssignedUser(workerInfo.parallelIndex));
    },
    { scope: 'worker' }
  ],

  appReady: [
    async ({ page }, use) => {
      await page.goto('/');
      await use();
    },
    { auto: true }
  ],

  consoleLog: [
    async ({ page }, use, testInfo) => {
      const lines: string[] = [];
      page.on('console', (msg) => lines.push(`[${msg.type()}] ${msg.text()}`));

      await use(lines);

      // Runs during teardown, after the test body has finished and its status is known. Everything else
      // the sibling's `attachFailureEvidence` collected by hand - screenshot, page source, final URL - is
      // captured by Playwright itself via the `screenshot`/`trace` settings in playwright.config.ts, so
      // the console log is the only piece left worth wiring up manually.
      if (testInfo.status !== testInfo.expectedStatus && lines.length > 0) {
        await testInfo.attach('browser-console-log', {
          body: lines.join('\n'),
          contentType: 'text/plain'
        });
      }
    },
    { auto: true }
  ],

  activeUser: async ({ assignedUser, userOverride, log }, use, testInfo) => {
    const user = userOverride ? userByUsername(userOverride) : assignedUser;
    log.info('Resolved the account for this test', {
      username: user.username,
      source: userOverride ? 'userOverride' : 'worker assignment',
      parallelIndex: testInfo.parallelIndex
    });

    // Recorded for both reporters: annotations surface in the Playwright HTML report, allure parameters
    // in the Allure report.
    testInfo.annotations.push({ type: 'user', description: user.username });
    testInfo.annotations.push({ type: 'parallelIndex', description: String(testInfo.parallelIndex) });
    await allure.parameter('user', user.username);
    await allure.parameter('parallelIndex', String(testInfo.parallelIndex));

    await use(user);
  },

  // Same load-bearing empty pattern as `assignedUser` above: Playwright reads a fixture's dependencies
  // out of this destructuring, so it cannot be simplified to `_`.
  // eslint-disable-next-line no-empty-pattern
  log: async ({}, use, testInfo) => {
    // testId, not the title: titles repeat across projects, and a data-driven loop can generate several
    // tests whose titles differ only by an interpolated value.
    const logger = new TestLogger(testInfo.testId);

    await use(logger);

    // Teardown. A passing test's log is cost with no reader, so it is attached only on failure -
    // LOG_ATTACH=always overrides that when a passing-but-suspicious test needs inspecting.
    const failed = testInfo.status !== testInfo.expectedStatus;
    if (logger.entryCount > 0 && (failed || process.env.LOG_ATTACH === 'always')) {
      await testInfo.attach('test-log', {
        body: logger.toJsonLines(),
        // JSON Lines is not valid JSON as a whole document, so labelling it application/json would make
        // report viewers try and fail to pretty-print it.
        contentType: 'text/plain'
      });
    }
  },

  loginPage: async ({ page, log }, use) => {
    await use(new LoginPage(page, log));
  },

  inventoryPage: async ({ page, log }, use) => {
    await use(new InventoryPage(page, log));
  },

  // `appReady` is listed explicitly rather than relied on implicitly: it is an auto fixture, so it would
  // run anyway, but naming it here is what guarantees navigation completes *before* this one logs in.
  loggedIn: async ({ appReady, loginPage, inventoryPage, activeUser, log }, use) => {
    void appReady;
    log.info('Signing in', { username: activeUser.username });
    await loginPage.submitLogin(activeUser.username, activeUser.password);
    await inventoryPage.clearCart();
    log.info('Signed in with an empty cart');
    await use(inventoryPage);
  }
});

export { expect } from '@playwright/test';
