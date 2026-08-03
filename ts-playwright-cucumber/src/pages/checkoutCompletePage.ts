import type { Page } from 'playwright';
import { BasePage } from './basePage.ts';

const COMPLETE_HEADER = "[data-test='complete-header']";

export class CheckoutCompletePage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  getConfirmationMessage(): Promise<string> {
    return this.textOf(COMPLETE_HEADER);
  }
}
