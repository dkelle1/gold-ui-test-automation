import { setWorldConstructor, World as CucumberWorld } from '@cucumber/cucumber';
import type { Page } from 'playwright';
import type { ScenarioSession } from './browser/browserManager.ts';
import type { UserAccount } from './users/userAccount.ts';

/**
 * Cucumber.js's equivalent of Reqnroll's scenario-scoped BoDi container: one fresh instance per
 * scenario, bound to `this` inside every step definition and `Before`/`After` hook on it. `session` and
 * `userAccount` are set by the `Before` hook in hooks.ts, before any step runs.
 */
export class SauceDemoWorld extends CucumberWorld {
  session?: ScenarioSession;
  userAccount?: UserAccount;

  get page(): Page {
    if (!this.session) {
      throw new Error(
        'No active scenario session - page is only available from within a Before/step/After hook.'
      );
    }
    return this.session.page;
  }

  get user(): UserAccount {
    if (!this.userAccount) {
      throw new Error('No user assigned yet - user is only available after the Before hook has run.');
    }
    return this.userAccount;
  }
}

setWorldConstructor(SauceDemoWorld);
