import type { Page } from 'playwright';
import { BasePage } from './basePage.ts';
import { CheckoutCompletePage } from './checkoutCompletePage.ts';

const ITEM_NAMES = "[data-test='inventory-item-name']";
const TOTAL_LABEL = "[data-test='total-label']";
const FINISH_BUTTON = "[data-test='finish']";

export class CheckoutOverviewPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  listItemNames(): Promise<string[]> {
    return this.page.locator(ITEM_NAMES).allTextContents();
  }

  getTotalLabel(): Promise<string> {
    return this.textOf(TOTAL_LABEL);
  }

  async finish(): Promise<CheckoutCompletePage> {
    await this.click(FINISH_BUTTON);
    return new CheckoutCompletePage(this.page);
  }
}
