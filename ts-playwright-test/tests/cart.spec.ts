import { expect, test } from '../src/fixtures';
import { SAUCE_LABS_BACKPACK, SAUCE_LABS_BIKE_LIGHT } from '../src/testdata/productCatalog';

/**
 * The Playwright Test rendering of the sibling frameworks' Cart.feature.
 *
 * The `loggedIn` fixture is this file's `Background:` - logged in, product grid rendered, cart empty.
 * Requesting it by name in the test signature is what runs it; a test that does not need it (see
 * login.spec.ts) simply does not ask for it, with no tag or hook-filtering logic involved.
 */
test.describe('Cart', () => {
  test('adding and removing items updates the cart badge', { tag: ['@cart'] }, async ({ loggedIn }) => {
    await loggedIn.addToCart(SAUCE_LABS_BACKPACK);
    await loggedIn.addToCart(SAUCE_LABS_BIKE_LIGHT);

    // A web-first assertion, which retries until the badge shows this text or the expect timeout
    // elapses. The Cucumber sibling needs a hand-written `retryUntil` helper plus a `getCartCount()`
    // that catches a 500ms TimeoutError to model "no badge means zero" - both of which this replaces.
    await expect(loggedIn.cartBadge).toHaveText('2');

    await loggedIn.removeFromCart(SAUCE_LABS_BACKPACK);
    await expect(loggedIn.cartBadge).toHaveText('1');

    await loggedIn.removeFromCart(SAUCE_LABS_BIKE_LIGHT);
    // saucedemo removes the badge from the DOM entirely at zero, so this is the empty-cart assertion -
    // no sentinel value, no swallowed timeout.
    await expect(loggedIn.cartBadge).toBeHidden();
  });

  test('items added to the cart are listed in the cart', { tag: ['@cart'] }, async ({ loggedIn }) => {
    const expected = [SAUCE_LABS_BACKPACK, SAUCE_LABS_BIKE_LIGHT];

    await loggedIn.addToCart(SAUCE_LABS_BACKPACK);
    await loggedIn.addToCart(SAUCE_LABS_BIKE_LIGHT);

    const cartPage = await loggedIn.openCart();

    // `expect.poll` retries an arbitrary async function the way a web-first assertion retries a locator -
    // the built-in that makes the sibling's `support/retryUntil.ts` unnecessary here. Sorted on both
    // sides deliberately: the assertion is about cart *contents*, not display order, so it should not
    // fail if saucedemo ever reorders the list.
    await expect
      .poll(async () => (await cartPage.itemNames.allTextContents()).slice().sort())
      .toEqual(expected.slice().sort());
  });
});
