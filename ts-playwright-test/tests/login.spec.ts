import { expect, test } from '../src/fixtures';
import { createInvalidCredentials } from '../src/testdata/invalidCredentialsFactory';
import { ALL_PRODUCTS } from '../src/testdata/productCatalog';
import { standardUser } from '../src/users/userCatalog';

/**
 * The Playwright Test rendering of the sibling frameworks' Login.feature. Same coverage, same
 * assertions, no Gherkin layer - see this framework's README for the side-by-side comparison.
 */
test.describe('Login', () => {
  test(
    'a pooled user can log in and see the full product grid',
    { tag: ['@smoke', '@login'] },
    async ({ loginPage, inventoryPage, activeUser }) => {
      await loginPage.submitLogin(activeUser.username, activeUser.password);

      await expect(inventoryPage.productNames).toHaveCount(ALL_PRODUCTS.length);

      const listed = await inventoryPage.productNames.allTextContents();
      const unknown = listed.filter((name) => !ALL_PRODUCTS.includes(name));
      expect(unknown, 'Every listed product name should be a known saucedemo catalog product.').toEqual([]);
    }
  );

  test.describe('a locked-out user', () => {
    // The Playwright Test equivalent of the sibling's `@user:locked_out_user` tag: scoped by describe
    // block, type-checked, and resolved by the `activeUser` fixture rather than by parsing tag strings.
    test.use({ userOverride: 'locked_out_user' });

    test(
      'is rejected at login with the lockout message',
      { tag: ['@negative', '@login'] },
      async ({ loginPage, activeUser }) => {
        await loginPage.submitLogin(activeUser.username, activeUser.password);

        await expect(loginPage.errorBanner).toHaveText('Epic sadface: Sorry, this user has been locked out.');
      }
    );
  });

  /**
   * The `Scenario Outline` / `Examples` table, expressed as an ordinary loop over data.
   *
   * Worth noting what is gained and what is lost. Gained: the cases are real typed values, a case can
   * compute its own data (`generate: true` below) instead of smuggling a `<generated>` sentinel string
   * through the table and unpacking it in a step definition, and the test title is built from the data.
   * Lost: a non-programmer can no longer read or edit the table. That trade is the whole argument
   * between this framework and its Cucumber sibling, and neither answer is universally right.
   */
  const invalidLoginCases = [
    { description: 'a blank username', username: '', password: standardUser.password, generate: false },
    { description: 'a blank password', username: standardUser.username, password: '', generate: false },
    { description: 'credentials matching no real account', username: '', password: '', generate: true }
  ];

  for (const testCase of invalidLoginCases) {
    test(
      `login is rejected for ${testCase.description}`,
      { tag: ['@negative', '@login'] },
      async ({ loginPage }) => {
        const { username, password } = testCase.generate ? createInvalidCredentials() : testCase;

        await loginPage.submitLogin(username, password);

        await expect(loginPage.errorBanner).toBeVisible();
      }
    );
  }
});
