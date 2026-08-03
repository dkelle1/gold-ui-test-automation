import { errors } from 'playwright';
import type { Page } from 'playwright';
import { BasePage } from './basePage.ts';
import { CartPage } from './cartPage.ts';

const PRODUCT_NAMES = "[data-test='inventory-item-name']";
const CART_BADGE = "[data-test='shopping-cart-badge']";
const CART_LINK = "[data-test='shopping-cart-link']";

// XPath by CSS class + visible button text, carried over unchanged from both siblings' InventoryPage
// (verified there against the live saucedemo DOM during the Selenium framework's own CI debugging):
// each product sits in <div class="inventory_item"> containing <div data-test="inventory-item-name">,
// and the toggle is <button>Add to cart</button> / <button>Remove</button>. Playwright locators support
// `xpath=` natively, so this needed no translation.
function addToCartButtonFor(productName: string): string {
  return `xpath=//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='${productName}']]//button[text()='Add to cart']`;
}

function removeFromCartButtonFor(productName: string): string {
  return `xpath=//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='${productName}']]//button[text()='Remove']`;
}

export class InventoryPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  // PRODUCT_NAMES matches all six products on the grid, so any wait/visibility check built on it must
  // use the *AnyVisible family (see BasePage) rather than the strict single-match variants - Playwright
  // throws a "strict mode violation" otherwise, since (unlike Selenium's FindElement) its locator
  // actions refuse to silently pick the first of several matches.
  isDisplayed(): Promise<boolean> {
    return this.waitAndCheckAnyVisible(PRODUCT_NAMES);
  }

  listProductNames(): Promise<string[]> {
    return this.page.locator(PRODUCT_NAMES).allTextContents();
  }

  addToCart(productName: string): Promise<void> {
    return this.clickAndConfirmToggle(
      addToCartButtonFor(productName),
      removeFromCartButtonFor(productName),
      productName,
      'added to'
    );
  }

  removeFromCart(productName: string): Promise<void> {
    return this.clickAndConfirmToggle(
      removeFromCartButtonFor(productName),
      addToCartButtonFor(productName),
      productName,
      'removed from'
    );
  }

  /**
   * Establishes an empty cart as a scenario precondition, and (because it waits for the product grid
   * first) doubles as the "login has finished rendering" barrier that `LoginPage.submitLogin` does not
   * provide on its own.
   *
   * In practice the removal loop is normally a no-op: saucedemo keeps the cart client-side, and every
   * scenario gets a brand-new, fully isolated `BrowserContext` (see browserManager.ts), so nothing
   * carries over between scenarios or runs. It is kept as an explicit, cheap guard so a scenario
   * asserting exact cart contents can never inherit unexpected state.
   */
  async clearCart(): Promise<void> {
    await this.waitForAnyVisible(PRODUCT_NAMES);

    for (const productName of await this.listProductNames()) {
      if (await this.isVisible(removeFromCartButtonFor(productName))) {
        await this.removeFromCart(productName);
      }
    }
  }

  /**
   * The number on the cart badge, or 0 when no badge is rendered - which is how saucedemo shows an
   * empty cart. A check-then-read pair would leave a window where the badge disappears between the two
   * calls and the read then blocks for the full context timeout before throwing - failing the assertion
   * instead of simply reporting 0 - so this instead makes one read with a short timeout of its own and
   * treats "never showed up that quickly" as absence. Callers poll this repeatedly (see the cart-badge
   * step definitions' retry helper), so it must stay cheap on the common "badge isn't there" path.
   */
  async getCartCount(): Promise<number> {
    try {
      const text = await this.page.locator(CART_BADGE).textContent({ timeout: 500 });
      const count = text ? Number.parseInt(text, 10) : Number.NaN;
      return Number.isNaN(count) ? 0 : count;
    } catch (error) {
      if (error instanceof errors.TimeoutError) {
        return 0;
      }
      throw error;
    }
  }

  async openCart(): Promise<CartPage> {
    await this.click(CART_LINK);
    return new CartPage(this.page);
  }

  /**
   * Clicks a toggle button (add-to-cart/remove) and confirms the click actually took effect by waiting
   * for its counterpart button to appear, retrying a few times if not. Kept as deliberate defense in
   * depth, not because Playwright has the click-didn't-register race Selenium's WebDriver click could -
   * its stronger actionability checks (attached, visible, stable, and actually hit-testable at the click
   * point) make that specific race meaningfully less likely already. The Selenium sibling's own
   * investigation of its remaining CI flakiness concluded the most likely cause was saucedemo.com itself
   * degrading under repeated automated traffic, not a client-side timing bug - and no client library,
   * however good its waiting, can wait out a server that never changes state.
   */
  private async clickAndConfirmToggle(
    clickSelector: string,
    counterpartSelector: string,
    productName: string,
    action: string
  ): Promise<void> {
    const maxAttempts = 3;
    const confirmTimeoutMs = 3000;
    let clicks = 0;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      // The first attempt always clicks - click()'s own actionability wait is what carries the "wait for
      // a not-yet-rendered grid" job. Later attempts only re-click if the toggle button is still in its
      // "before" state: if an earlier attempt actually did land (just slower than confirmTimeoutMs), the
      // button will already have flipped, and re-clicking it would mean waiting out the full default
      // timeout for nothing.
      if (attempt === 1 || (await this.isVisible(clickSelector))) {
        await this.click(clickSelector);
        clicks++;
      }

      if (await this.waitAndCheckVisible(counterpartSelector, confirmTimeoutMs)) {
        return;
      }
    }

    // Report the real click count. Saying "clicked 3 times" when the button was never in a clickable
    // state (so nothing was clicked at all) points debugging at the app instead of at the page state,
    // which is exactly backwards.
    throw new Error(
      `'${productName}' was never actually ${action} the cart: over ${maxAttempts} attempts the toggle ` +
        `button was clicked ${clicks} time(s) and its counterpart ('${counterpartSelector}') never appeared.`
    );
  }
}
