import type { Page } from 'playwright';
import { BasePage } from './basePage.ts';
import { CheckoutInformationPage } from './checkoutInformationPage.ts';

const CART_ITEM_NAMES = "[data-test='inventory-item-name']";
const CHECKOUT_BUTTON = "[data-test='checkout']";

export class CartPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  listItemNames(): Promise<string[]> {
    return this.page.locator(CART_ITEM_NAMES).allTextContents();
  }

  async startCheckout(): Promise<CheckoutInformationPage> {
    await this.click(CHECKOUT_BUTTON);
    return new CheckoutInformationPage(this.page);
  }
}
