import { Given, Then, When } from '@cucumber/cucumber';
import { expect } from 'chai';
import { LoginPage } from '../pages/loginPage.ts';
import { InventoryPage } from '../pages/inventoryPage.ts';
import { createInvalidCredentials } from '../testdata/invalidCredentialsFactory.ts';
import { ALL_PRODUCTS } from '../testdata/productCatalog.ts';
import type { SauceDemoWorld } from '../world.ts';

// Registered once via Given: Cucumber matches step definitions by text only, never by which of
// Given/When/Then/Step registered them nor which keyword a .feature file happens to use for a given
// step - so this one definition already covers both login.feature's "When I log in..." and
// checkout.feature/cart.feature's "Given I log in...". A separate `When(...)` registration for the
// identical text used to exist here too; Cucumber rightly refused to run ANY scenario using this step,
// reporting it as ambiguous, since both definitions were equally valid matches for every occurrence.
Given('I log in with my assigned user', async function (this: SauceDemoWorld) {
  await new LoginPage(this.page).submitLogin(this.user.username, this.user.password);
});

When(
  'I log in with username {string} and password {string}',
  async function (this: SauceDemoWorld, username: string, password: string) {
    // The "<generated>" marker lets an Examples row ask for fresh, guaranteed-not-real credentials
    // instead of a fixed literal.
    if (username === '<generated>' || password === '<generated>') {
      const generated = createInvalidCredentials();
      username = username === '<generated>' ? generated.username : username;
      password = password === '<generated>' ? generated.password : password;
    }

    await new LoginPage(this.page).submitLogin(username, password);
  }
);

Then('I should land on the inventory page', async function (this: SauceDemoWorld) {
  expect(
    await new InventoryPage(this.page).isDisplayed(),
    'Expected the inventory page to be displayed after login.'
  ).to.be.true;
});

Then('{int} products should be listed', async function (this: SauceDemoWorld, expectedCount: number) {
  const actual = await new InventoryPage(this.page).listProductNames();
  expect(actual).to.have.lengthOf(expectedCount);
  expect(actual, 'Every listed product name should be a known saucedemo catalog product.').to.satisfy(
    (names: string[]) => names.every((name) => ALL_PRODUCTS.includes(name))
  );
});

Then('I should see the login error {string}', async function (this: SauceDemoWorld, expectedMessage: string) {
  expect(await new LoginPage(this.page).getErrorMessage()).to.equal(expectedMessage);
});

Then('I should see a login error', async function (this: SauceDemoWorld) {
  expect(await new LoginPage(this.page).hasError()).to.be.true;
});
