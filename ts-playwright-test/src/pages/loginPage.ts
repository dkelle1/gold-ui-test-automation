import type { Locator, Page } from '@playwright/test';
import { BasePage } from './basePage';

export class LoginPage extends BasePage {
  private readonly usernameInput: Locator;
  private readonly passwordInput: Locator;
  private readonly loginButton: Locator;

  /**
   * Public because tests assert on it directly (`await expect(loginPage.errorBanner).toHaveText(...)`).
   * The Cucumber sibling instead exposes `getErrorMessage(): Promise<string>` and
   * `hasError(): Promise<boolean>`, because a step definition cannot assert on a Locator - it needs a
   * plain value. Handing out the Locator is what lets the assertion retry.
   */
  readonly errorBanner: Locator;

  constructor(page: Page) {
    super(page);
    this.usernameInput = page.getByTestId('username');
    this.passwordInput = page.getByTestId('password');
    this.loginButton = page.getByTestId('login-button');
    this.errorBanner = page.getByTestId('error');
  }

  async goto(): Promise<void> {
    await this.page.goto('/');
  }

  /** Submits the login form without asserting the outcome - the caller decides whether success or a specific error is expected. */
  async submitLogin(username: string, password: string): Promise<void> {
    await this.usernameInput.fill(username);
    await this.passwordInput.fill(password);
    await this.loginButton.click();
  }
}
