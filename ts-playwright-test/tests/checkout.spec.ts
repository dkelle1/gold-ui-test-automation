import { expect, test } from '../src/fixtures';
import { createCheckoutInfo } from '../src/testdata/checkoutDataFactory';
import {
  SAUCE_LABS_BACKPACK,
  SAUCE_LABS_BIKE_LIGHT,
  SAUCE_LABS_FLEECE_JACKET,
  SAUCE_LABS_ONESIE
} from '../src/testdata/productCatalog';

const TOTAL_LABEL_PATTERN = /^Total: \$(\d+\.\d{2})$/;

/** The Playwright Test rendering of the sibling frameworks' Checkout.feature. */
test.describe('Checkout', () => {
  test(
    'a single product can be purchased end to end',
    { tag: ['@smoke', '@e2e', '@checkout'] },
    async ({ loggedIn }) => {
      await loggedIn.addToCart(SAUCE_LABS_BACKPACK);

      const cartPage = await loggedIn.openCart();
      const informationPage = await cartPage.startCheckout();
      const overviewPage = await informationPage.fillAndContinue(createCheckoutInfo());

      // Shape first, as a retrying assertion, so a still-rendering total reads as "not yet" rather than
      // as a parse failure...
      await expect(overviewPage.totalLabel).toHaveText(TOTAL_LABEL_PATTERN);

      // ...then the numeric check, which needs the parsed value and so cannot be a locator assertion.
      const totalText = (await overviewPage.totalLabel.textContent()) ?? '';
      const amount = Number.parseFloat(TOTAL_LABEL_PATTERN.exec(totalText)?.[1] ?? '0');
      expect(amount, `Order total parsed from '${totalText}' should be positive.`).toBeGreaterThan(0);

      const completePage = await overviewPage.finish();
      await expect(completePage.confirmationHeader).toHaveText('Thank you for your order!');
    }
  );

  const multiProductCases = [
    { productA: SAUCE_LABS_BACKPACK, productB: SAUCE_LABS_BIKE_LIGHT },
    { productA: SAUCE_LABS_ONESIE, productB: SAUCE_LABS_FLEECE_JACKET }
  ];

  for (const { productA, productB } of multiProductCases) {
    test(
      `two products can be purchased together: ${productA} + ${productB}`,
      { tag: ['@e2e', '@checkout'] },
      async ({ loggedIn }) => {
        await loggedIn.addToCart(productA);
        await loggedIn.addToCart(productB);

        const cartPage = await loggedIn.openCart();
        const informationPage = await cartPage.startCheckout();
        const overviewPage = await informationPage.fillAndContinue(createCheckoutInfo());

        await expect(overviewPage.itemNames).toHaveCount(2);

        const completePage = await overviewPage.finish();
        await expect(completePage.confirmationHeader).toHaveText('Thank you for your order!');
      }
    );
  }

  test(
    'checkout is rejected without a postal code',
    { tag: ['@negative', '@checkout'] },
    async ({ loggedIn }) => {
      await loggedIn.addToCart(SAUCE_LABS_BACKPACK);

      const cartPage = await loggedIn.openCart();
      const informationPage = await cartPage.startCheckout();
      await informationPage.submitWithoutPostalCode(createCheckoutInfo());

      await expect(informationPage.errorBanner).toHaveText('Error: Postal Code is required');
    }
  );

  test.describe('problem_user', () => {
    test.use({ userOverride: 'problem_user' });

    /**
     * A real, previously-identified saucedemo defect, not a test bug: problem_user's checkout form
     * silently discards the last-name value, so a fully-filled form still reports the field as missing.
     * Tagged `@known-issue` and asserted explicitly, exactly as in the sibling frameworks - the point is
     * that the defect is pinned down, so a change in its behaviour shows up as a failure.
     */
    test(
      'cannot complete checkout because the last name field is broken',
      { tag: ['@negative', '@checkout', '@known-issue'] },
      async ({ loggedIn }) => {
        await loggedIn.addToCart(SAUCE_LABS_BACKPACK);

        const cartPage = await loggedIn.openCart();
        const informationPage = await cartPage.startCheckout();
        await informationPage.fillAndContinue(createCheckoutInfo());

        await expect(informationPage.errorBanner).toHaveText('Error: Last Name is required');
      }
    );
  });
});
