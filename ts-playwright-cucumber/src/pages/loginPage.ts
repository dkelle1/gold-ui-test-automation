import type { Page } from 'playwright';
import { BasePage } from './basePage.ts';

const USERNAME_INPUT = "[data-test='username']";
const PASSWORD_INPUT = "[data-test='password']";
const LOGIN_BUTTON = "[data-test='login-button']";
const ERROR_BANNER = "[data-test='error']";

export class LoginPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  /** Submits the login form without asserting the outcome - the caller decides whether success or a specific error is expected. */
  async submitLogin(username: string, password: string): Promise<void> {
    await this.type(USERNAME_INPUT, username);
    await this.type(PASSWORD_INPUT, password);
    await this.click(LOGIN_BUTTON);
  }

  getErrorMessage(): Promise<string> {
    return this.textOf(ERROR_BANNER);
  }

  hasError(): Promise<boolean> {
    return this.waitAndCheckVisible(ERROR_BANNER);
  }
}
