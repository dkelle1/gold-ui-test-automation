import { DataTable, Then, When } from '@cucumber/cucumber';
import { expect } from 'chai';
import { CartPage } from '../pages/cartPage.ts';
import { InventoryPage } from '../pages/inventoryPage.ts';
import { retryUntil } from '../support/retryUntil.ts';
import type { SauceDemoWorld } from '../world.ts';

Then('the cart badge should show {int}', async function (this: SauceDemoWorld, expectedCount: number) {
  const inventoryPage = new InventoryPage(this.page);
  const count = await retryUntil(
    () => inventoryPage.getCartCount(),
    (c) => c === expectedCount
  );
  expect(count).to.equal(expectedCount);
});

When('I remove {string} from the cart', async function (this: SauceDemoWorld, productName: string) {
  await new InventoryPage(this.page).removeFromCart(productName);
});

When('I start checkout', async function (this: SauceDemoWorld) {
  await new CartPage(this.page).startCheckout();
});

Then('the cart should list the following products:', async function (this: SauceDemoWorld, table: DataTable) {
  const expected = table.hashes().map((row) => row.ProductName ?? '');
  const cartPage = new CartPage(this.page);
  const names = await retryUntil(
    () => cartPage.listItemNames(),
    (actual) => actual.length === expected.length && actual.every((name) => expected.includes(name))
  );
  expect(names).to.have.members(expected);
});
