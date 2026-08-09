import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';
import { CheckoutCompletePage } from './checkoutCompletePage';

export class CheckoutOverviewPage extends BasePage {
  readonly itemNames: Locator;
  readonly totalLabel: Locator;
  private readonly finishButton: Locator;

  constructor(page: Page) {
    super(page);
    this.itemNames = page.getByTestId('inventory-item-name');
    this.totalLabel = page.getByTestId('total-label');
    this.finishButton = page.getByTestId('finish');
  }

  async finish(): Promise<CheckoutCompletePage> {
    await this.finishButton.click();
    return new CheckoutCompletePage(this.page);
  }
}
