import { Then, When } from '@cucumber/cucumber';
import { expect } from 'chai';
import { CheckoutCompletePage } from '../pages/checkoutCompletePage.ts';
import { CheckoutInformationPage } from '../pages/checkoutInformationPage.ts';
import { CheckoutOverviewPage } from '../pages/checkoutOverviewPage.ts';
import { createCheckoutInfo } from '../testdata/checkoutDataFactory.ts';
import { retryUntil } from '../support/retryUntil.ts';
import type { SauceDemoWorld } from '../world.ts';

const TOTAL_LABEL_PATTERN = /^Total: \$(\d+\.\d{2})$/;

When('I fill in my checkout information', async function (this: SauceDemoWorld) {
  await new CheckoutInformationPage(this.page).fillAndContinue(createCheckoutInfo());
});

When('I fill in my checkout information without a postal code', async function (this: SauceDemoWorld) {
  const info = { ...createCheckoutInfo(), postalCode: '' };
  await new CheckoutInformationPage(this.page).submitWithoutPostalCode(info);
});

Then('the overview should list {int} items', async function (this: SauceDemoWorld, expectedCount: number) {
  const overviewPage = new CheckoutOverviewPage(this.page);
  const names = await retryUntil(
    () => overviewPage.listItemNames(),
    (actual) => actual.length === expectedCount
  );
  expect(names).to.have.lengthOf(expectedCount);
});

Then('the order total should be a positive dollar amount', async function (this: SauceDemoWorld) {
  const totalLabel = await new CheckoutOverviewPage(this.page).getTotalLabel();
  const match = TOTAL_LABEL_PATTERN.exec(totalLabel);

  expect(match, `'${totalLabel}' did not match the expected 'Total: $<amount>' format.`).to.not.be.null;
  expect(Number.parseFloat(match![1] ?? '0')).to.be.greaterThan(0);
});

When('I finish the order', async function (this: SauceDemoWorld) {
  await new CheckoutOverviewPage(this.page).finish();
});

Then(
  'I should see the order confirmation {string}',
  async function (this: SauceDemoWorld, expectedMessage: string) {
    expect(await new CheckoutCompletePage(this.page).getConfirmationMessage()).to.equal(expectedMessage);
  }
);

Then(
  'I should see the checkout error {string}',
  async function (this: SauceDemoWorld, expectedMessage: string) {
    expect(await new CheckoutInformationPage(this.page).getErrorMessage()).to.equal(expectedMessage);
  }
);
