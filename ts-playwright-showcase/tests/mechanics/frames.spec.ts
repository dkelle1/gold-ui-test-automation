import { expect, test } from '@playwright/test';

/**
 * Roadmap item: **Frames**.
 *
 * saucedemo.com has no iframes at all, so none of the four sibling frameworks can demonstrate this.
 *
 * The thing worth internalising: a `Locator` never crosses a frame boundary on its own. `page.locator()`
 * searches the top-level document only, so an element inside an iframe is simply not found - which
 * surfaces as an ordinary "element not visible" timeout and sends people hunting for a race condition
 * that does not exist. `frameLocator()` is the boundary crossing, and it chains for nesting.
 */
test.describe('Frames', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/frames.html');
  });

  test('a locator does not cross into a frame by itself', async ({ page }) => {
    await expect(page.getByTestId('top-level-marker')).toBeVisible();

    // The inner paragraph exists in the DOM tree of a nested document, but not in this one.
    await expect(page.getByTestId('inner-frame-marker')).toHaveCount(0);
  });

  test('frameLocator chains through nested frames', async ({ page }) => {
    const outer = page.frameLocator('[data-test="outer-frame"]');
    await expect(outer.getByTestId('outer-frame-marker')).toBeVisible();

    const inner = outer.frameLocator('[data-test="inner-frame"]');
    await expect(inner.getByTestId('inner-frame-marker')).toHaveText('This paragraph is in the INNER frame.');
  });

  test('a form inside a nested frame can be driven end to end', async ({ page }) => {
    const inner = page.frameLocator('[data-test="outer-frame"]').frameLocator('[data-test="inner-frame"]');

    await inner.getByTestId('inner-message').fill('hello from the test');
    await inner.getByTestId('inner-submit').click();

    await expect(inner.getByTestId('inner-result')).toHaveText('inner frame received: hello from the test');
  });
});
