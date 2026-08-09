import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';
import { CheckoutInformationPage } from './checkoutInformationPage';

export class CartPage extends BasePage {
  readonly itemNames: Locator;
  private readonly checkoutButton: Locator;

  constructor(page: Page) {
    super(page);
    this.itemNames = page.getByTestId('inventory-item-name');
    this.checkoutButton = page.getByTestId('checkout');
  }

  async startCheckout(): Promise<CheckoutInformationPage> {
    await this.checkoutButton.click();
    return new CheckoutInformationPage(this.page);
  }
}
