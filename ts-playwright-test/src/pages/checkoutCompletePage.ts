import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';
import type { TestLogger } from '../support/testLogger';

export class CheckoutCompletePage extends BasePage {
  readonly confirmationHeader: Locator;

  constructor(page: Page, log: TestLogger) {
    super(page, log);
    this.confirmationHeader = page.getByTestId('complete-header');
  }
}
