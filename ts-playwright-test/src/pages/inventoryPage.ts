import { expect } from '@playwright/test';
import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';
import { CartPage } from './cartPage';

export class InventoryPage extends BasePage {
  /** All six product-name cells on the grid. Tests assert on `.count()` / `allTextContents()` via web-first assertions. */
  readonly productNames: Locator;
  /** Absent from the DOM entirely when the cart is empty - `await expect(cartBadge).toBeHidden()` is the empty-cart assertion. */
  readonly cartBadge: Locator;
  private readonly cartLink: Locator;

  constructor(page: Page) {
    super(page);
    this.productNames = page.getByTestId('inventory-item-name');
    this.cartBadge = page.getByTestId('shopping-cart-badge');
    this.cartLink = page.getByTestId('shopping-cart-link');
  }

  /**
   * One product tile, found by filtering the grid down to the tile containing an exact-text match for
   * the product name.
   *
   * This replaces the interpolated XPath the three sibling frameworks share
   * (`//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='<name>']]//button[text()='Add to cart']`).
   * The two gains are worth being explicit about: string-interpolating a product name into an XPath
   * predicate breaks on any name containing a quote, and matching a button by `text()` in XPath is a
   * structural match that says nothing about the element being a button at all. `getByRole('button')`
   * below asserts the accessible role, which is both what a user perceives and what survives markup
   * changes that leave the role intact.
   *
   * The `.inventory_item` container class is the one DOM fact carried over unchanged - it was verified
   * against the live saucedemo DOM during the Selenium sibling's CI debugging, so it is not a guess.
   */
  private itemFor(productName: string): Locator {
    return this.page
      .locator('.inventory_item')
      .filter({ has: this.page.getByText(productName, { exact: true }) });
  }

  // `exact: true` because getByRole's accessible-name matching is substring-based by default. The two
  // saucedemo button labels do not overlap, so substring matching would work today - but an exact match
  // is what the sibling frameworks' verified XPath predicate (`//button[text()='Add to cart']`) actually
  // asserts, so this keeps the migrated locator equivalent rather than looser.
  private addButtonFor(productName: string): Locator {
    return this.itemFor(productName).getByRole('button', { name: 'Add to cart', exact: true });
  }

  private removeButtonFor(productName: string): Locator {
    return this.itemFor(productName).getByRole('button', { name: 'Remove', exact: true });
  }

  async addToCart(productName: string): Promise<void> {
    await this.toggle(
      this.addButtonFor(productName),
      this.removeButtonFor(productName),
      productName,
      'added to'
    );
  }

  async removeFromCart(productName: string): Promise<void> {
    await this.toggle(
      this.removeButtonFor(productName),
      this.addButtonFor(productName),
      productName,
      'removed from'
    );
  }

  /**
   * Establishes an empty cart as a test precondition, and (because it waits for the product grid first)
   * doubles as the "login has finished rendering" barrier that `LoginPage.submitLogin` does not provide.
   *
   * Normally a no-op: saucedemo keeps the cart client-side and every test gets a fresh, isolated
   * `BrowserContext`, so nothing carries over. Kept as a cheap explicit guard so a test asserting exact
   * cart contents can never inherit unexpected state.
   */
  async clearCart(): Promise<void> {
    await expect(this.productNames.first()).toBeVisible();

    for (const productName of await this.productNames.allTextContents()) {
      if (await this.removeButtonFor(productName).isVisible()) {
        await this.removeFromCart(productName);
      }
    }
  }

  async openCart(): Promise<CartPage> {
    await this.cartLink.click();
    return new CartPage(this.page);
  }

  /**
   * Clicks a toggle button (add-to-cart / remove) and confirms the click took effect by waiting for its
   * counterpart to appear, re-clicking a few times if not.
   *
   * Retained deliberately from the sibling frameworks rather than dropped as redundant. Playwright's
   * actionability checks do make a lost click much less likely than Selenium's, but the Selenium
   * sibling's own investigation concluded its residual CI flakiness was most likely saucedemo.com
   * degrading under repeated automated traffic - and no amount of client-side waiting fixes a server
   * that never changes state. This is the one hand-rolled retry in this framework, and it retries an
   * *action*, which is why a web-first assertion cannot replace it: `expect().toBeVisible()` re-checks,
   * it does not re-click.
   *
   * `isVisible()` (instant, non-retrying) is the right check on later attempts: if an earlier click
   * actually landed, the button has already flipped, and re-clicking would mean waiting out a full
   * timeout for nothing.
   */
  private async toggle(
    clickTarget: Locator,
    counterpart: Locator,
    productName: string,
    action: string
  ): Promise<void> {
    const maxAttempts = 3;
    const confirmTimeoutMs = 3000;
    let clicks = 0;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      if (attempt === 1 || (await clickTarget.isVisible())) {
        await clickTarget.click();
        clicks++;
      }

      try {
        await counterpart.waitFor({ state: 'visible', timeout: confirmTimeoutMs });
        return;
      } catch {
        // Not yet - fall through to the next attempt. A genuinely ambiguous locator would have thrown a
        // strict-mode violation on the click above, before ever reaching this wait.
      }
    }

    // Report the real click count. Saying "clicked 3 times" when the button was never in a clickable
    // state (so nothing was clicked at all) points debugging at the app instead of at the page state,
    // which is exactly backwards.
    throw new Error(
      `'${productName}' was never actually ${action} the cart: over ${maxAttempts} attempts the toggle ` +
        `button was clicked ${clicks} time(s) and its counterpart never appeared.`
    );
  }
}
