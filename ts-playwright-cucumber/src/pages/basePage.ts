import { errors } from 'playwright';
import type { Locator, Page } from 'playwright';

/**
 * Base class for saucedemo page objects. Playwright locators are lazy (they re-query the DOM on every
 * use, so there is no stale-element concept at all) and every action auto-waits for the target to be
 * attached, visible, stable and actionable before doing anything - so, as in the C# Playwright sibling,
 * there is no "WaitForClickable, then Click, retrying on staleness" dance to reimplement here. What's
 * left is two small helpers for the two positive-assertion shapes step definitions actually need: "wait
 * for this or fail loudly" and "wait for this and tell me whether it showed up, without throwing".
 *
 * A note on error types worth getting right, verified directly against a live browser rather than
 * assumed: a timed-out wait throws `errors.TimeoutError` (a real, distinctly-named class - unlike the
 * .NET binding, which has no such type and reuses `System.TimeoutException`). A strict-mode violation
 * (the selector matched more than one element) throws a plain `Error` whose message contains "strict
 * mode violation" - it is NOT a `TimeoutError`. That distinction is load-bearing for
 * `waitAndCheckVisible`: "the element never showed up" is a normal, reportable outcome that method
 * exists to turn into `false`, whereas "this selector is ambiguous" is a bug in the locator that should
 * be surfaced loudly, not absorbed into a bare `false` that reads as an ordinary assertion failure.
 */
export abstract class BasePage {
  // Not a constructor-parameter-property shorthand on purpose: Node's native TypeScript support (which
  // is what actually loads this file inside a Cucumber `--parallel` worker thread - see cucumber.js's
  // doc comment) only strips erasable syntax and explicitly rejects parameter properties with
  // ERR_UNSUPPORTED_TYPESCRIPT_SYNTAX, since they require real code generation (an implicit `this.page =
  // page` assignment), not just type erasure. Confirmed directly by hitting that exact error.
  protected readonly page: Page;

  protected constructor(page: Page) {
    this.page = page;
  }

  protected click(selector: string): Promise<void> {
    return this.page.locator(selector).click();
  }

  /** Fills the field directly (not simulated keystrokes) - saucedemo's forms have no per-keystroke behaviour (autocomplete, incremental validation) that needs real typing. */
  protected type(selector: string, text: string): Promise<void> {
    return this.page.locator(selector).fill(text);
  }

  protected async textOf(selector: string): Promise<string> {
    return (await this.page.locator(selector).textContent()) ?? '';
  }

  /** Instant, non-waiting presence check - correct where absence is a normal outcome (e.g. an empty cart badge), not a race to wait out. */
  protected isVisible(selector: string): Promise<boolean> {
    return this.page.locator(selector).isVisible();
  }

  /** Waits for the element to become visible, throwing a descriptive error if it never does - for preconditions where permanent absence really is a bug (e.g. the post-login product grid never rendering). */
  protected waitForVisible(selector: string): Promise<void> {
    return this.waitForVisibleLocator(this.page.locator(selector), selector);
  }

  /** Same as {@link waitForVisible}, but for a selector that legitimately matches several elements - see {@link waitAndCheckAnyVisible} for why that needs its own method. */
  protected waitForAnyVisible(selector: string): Promise<void> {
    return this.waitForVisibleLocator(this.page.locator(selector).first(), selector);
  }

  private async waitForVisibleLocator(locator: Locator, selector: string): Promise<void> {
    try {
      await locator.waitFor({ state: 'visible' });
    } catch (error) {
      // Re-throws the SAME error object (preserving its real type - TimeoutError or otherwise) with an
      // amended message, rather than wrapping it in a fresh generic Error, so a report reader still sees
      // whether this was a timeout or an ambiguous selector, not just "never became visible".
      if (error instanceof Error) {
        error.message = `Element '${selector}' never became visible. ${error.message}`;
      }
      throw error;
    }
  }

  /** Polls for the element to appear, returning false (never throwing) if it doesn't - for positive assertions ("an error banner should show up") where a brief render delay is expected but permanent absence is a real, reportable outcome rather than a bug in the wait. */
  protected waitAndCheckVisible(selector: string, timeoutMs?: number): Promise<boolean> {
    return this.waitAndCheckVisibleLocator(this.page.locator(selector), timeoutMs);
  }

  /** Same as {@link waitAndCheckVisible}, but asks only whether the *first* match is visible, for a selector that is genuinely expected to match several elements (e.g. the six product-name cells of the inventory grid) - see the class doc comment for why strict-mode locators need this separate method rather than disabling strict mode wholesale. */
  protected waitAndCheckAnyVisible(selector: string): Promise<boolean> {
    return this.waitAndCheckVisibleLocator(this.page.locator(selector).first(), undefined);
  }

  private async waitAndCheckVisibleLocator(
    locator: Locator,
    timeoutMs: number | undefined
  ): Promise<boolean> {
    try {
      await locator.waitFor({ state: 'visible', timeout: timeoutMs });
      return true;
    } catch (error) {
      if (error instanceof errors.TimeoutError) {
        return false;
      }
      // A strict-mode violation (or anything else) is a real bug, not "it never showed up" - let it propagate.
      throw error;
    }
  }
}
