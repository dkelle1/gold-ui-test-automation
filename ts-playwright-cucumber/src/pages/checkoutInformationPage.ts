import type { Page } from 'playwright';
import { BasePage } from './basePage.ts';
import { CheckoutOverviewPage } from './checkoutOverviewPage.ts';
import type { CheckoutInfo } from '../testdata/checkoutInfo.ts';

const FIRST_NAME_INPUT = "[data-test='firstName']";
const LAST_NAME_INPUT = "[data-test='lastName']";
const POSTAL_CODE_INPUT = "[data-test='postalCode']";
const CONTINUE_BUTTON = "[data-test='continue']";
const ERROR_BANNER = "[data-test='error']";

export class CheckoutInformationPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async fillAndContinue(info: CheckoutInfo): Promise<CheckoutOverviewPage> {
    await this.type(FIRST_NAME_INPUT, info.firstName);
    await this.type(LAST_NAME_INPUT, info.lastName);
    await this.type(POSTAL_CODE_INPUT, info.postalCode);
    await this.click(CONTINUE_BUTTON);
    return new CheckoutOverviewPage(this.page);
  }

  /** Leaves the postal code blank and submits, for the "postal code is required" negative scenario. */
  async submitWithoutPostalCode(info: CheckoutInfo): Promise<void> {
    await this.type(FIRST_NAME_INPUT, info.firstName);
    await this.type(LAST_NAME_INPUT, info.lastName);
    await this.click(CONTINUE_BUTTON);
  }

  getErrorMessage(): Promise<string> {
    return this.textOf(ERROR_BANNER);
  }

  hasError(): Promise<boolean> {
    return this.waitAndCheckVisible(ERROR_BANNER);
  }
}
