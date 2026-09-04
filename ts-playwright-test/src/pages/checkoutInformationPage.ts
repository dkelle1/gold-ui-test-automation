import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';
import type { TestLogger } from '../support/testLogger';
import { CheckoutOverviewPage } from './checkoutOverviewPage';
import type { CheckoutInfo } from '../testdata/checkoutInfo';

export class CheckoutInformationPage extends BasePage {
  private readonly firstNameInput: Locator;
  private readonly lastNameInput: Locator;
  private readonly postalCodeInput: Locator;
  private readonly continueButton: Locator;
  readonly errorBanner: Locator;

  constructor(page: Page, log: TestLogger) {
    super(page, log);
    this.firstNameInput = page.getByTestId('firstName');
    this.lastNameInput = page.getByTestId('lastName');
    this.postalCodeInput = page.getByTestId('postalCode');
    this.continueButton = page.getByTestId('continue');
    this.errorBanner = page.getByTestId('error');
  }

  async fillAndContinue(info: CheckoutInfo): Promise<CheckoutOverviewPage> {
    await this.firstNameInput.fill(info.firstName);
    await this.lastNameInput.fill(info.lastName);
    await this.postalCodeInput.fill(info.postalCode);
    await this.continueButton.click();
    return new CheckoutOverviewPage(this.page, this.log);
  }

  /** Leaves the postal code blank and submits, for the "postal code is required" negative test. */
  async submitWithoutPostalCode(info: CheckoutInfo): Promise<void> {
    await this.firstNameInput.fill(info.firstName);
    await this.lastNameInput.fill(info.lastName);
    await this.continueButton.click();
  }
}
