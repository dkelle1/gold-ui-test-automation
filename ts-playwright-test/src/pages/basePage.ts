import type { Page } from '@playwright/test';

/**
 * Base class for saucedemo page objects - deliberately almost empty, and that emptiness is the point.
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
 * for you and report the same distinction natively. Nothing is left to put in a base class except the
 * page handle itself.
 *
 * It is kept rather than deleted so the POM shape matches the sibling frameworks and there is an
 * obvious home for anything genuinely shared later.
 */
export abstract class BasePage {
  protected readonly page: Page;

  protected constructor(page: Page) {
    this.page = page;
  }
}
