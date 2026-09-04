import type { Page } from '@playwright/test';
import type { TestLogger } from '../support/testLogger';

/**
 * Base class for saucedemo page objects - deliberately minimal, and that minimalism is the point.
 *
 * The Cucumber sibling's `BasePage` is ~100 lines of wait helpers: `waitForVisible`,
 * `waitAndCheckVisible`, `waitForAnyVisible`, `waitAndCheckAnyVisible`, plus careful handling of the
 * difference between a `TimeoutError` ("it never showed up", a reportable outcome) and a strict-mode
 * violation ("this selector is ambiguous", a bug). All of it exists because a Cucumber step definition
 * has to reduce page state to a `boolean` or a `string` before it can assert on it, so the waiting must
 * happen inside the page object.
 *
 * Playwright Test removes the need for every one of those: page objects here expose `Locator`s, and the
 * test asserts on them with web-first assertions (`await expect(locator).toBeVisible()`), which retry
 * for you and report the same distinction natively.
 *
 * What is left is the two things every page genuinely needs - the page handle, and somewhere to record
 * what it did. Note what is still absent: not one wait helper.
 */
export abstract class BasePage {
  protected readonly page: Page;
  protected readonly log: TestLogger;

  protected constructor(page: Page, log: TestLogger) {
    this.page = page;
    this.log = log;
  }
}
