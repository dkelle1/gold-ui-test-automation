import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';
import type { TestLogger } from '../support/testLogger';
import { CheckoutInformationPage } from './checkoutInformationPage';

export class CartPage extends BasePage {
  readonly itemNames: Locator;
  private readonly checkoutButton: Locator;

  constructor(page: Page, log: TestLogger) {
    super(page, log);
    this.itemNames = page.getByTestId('inventory-item-name');
    this.checkoutButton = page.getByTestId('checkout');
  }

  async startCheckout(): Promise<CheckoutInformationPage> {
    await this.checkoutButton.click();
    return new CheckoutInformationPage(this.page, this.log);
  }
}
