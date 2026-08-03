import { DataTable, Given, When } from '@cucumber/cucumber';
import { InventoryPage } from '../pages/inventoryPage.ts';
import type { SauceDemoWorld } from '../world.ts';

When('I add {string} to the cart', async function (this: SauceDemoWorld, productName: string) {
  await new InventoryPage(this.page).addToCart(productName);
});

When('I add the following products to the cart:', async function (this: SauceDemoWorld, table: DataTable) {
  const inventoryPage = new InventoryPage(this.page);

  for (const row of table.hashes()) {
    await inventoryPage.addToCart(row.ProductName ?? '');
  }
});

When('I go to the cart', async function (this: SauceDemoWorld) {
  await new InventoryPage(this.page).openCart();
});

Given('my cart is empty', async function (this: SauceDemoWorld) {
  await new InventoryPage(this.page).clearCart();
});
