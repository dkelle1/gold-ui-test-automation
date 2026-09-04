import { expect, test } from '@playwright/test';

/**
 * Roadmap item: **Alerts** (native `alert` / `confirm` / `prompt` dialogs).
 *
 * The default that surprises people: with no `dialog` listener registered, Playwright **auto-dismisses**
 * every dialog. That is the opposite of Selenium, where an unhandled dialog blocks the session until
 * someone switches to it. The practical consequence is that a `confirm()` your test never mentions
 * silently answers "Cancel" - so the destructive action under test quietly does not happen, and the
 * assertion that it did fails somewhere far away from the cause.
 */
test.describe('Dialogs', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/dialogs.html');
  });

  test('an unhandled dialog is auto-dismissed rather than blocking the page', async ({ page }) => {
    // No page.on('dialog') here on purpose. The click resolves and the page keeps running, because
    // Playwright dismissed the alert for us.
    await page.getByTestId('trigger-alert').click();

    await expect(page.getByTestId('dialog-result')).toHaveText('alert acknowledged');
  });

  test('a dialog listener can read the message and accept', async ({ page }) => {
    const messages: string[] = [];
    page.on('dialog', async (dialog) => {
      messages.push(`${dialog.type()}: ${dialog.message()}`);
      await dialog.accept();
    });

    await page.getByTestId('trigger-confirm').click();

    await expect(page.getByTestId('dialog-result')).toHaveText('confirm accepted');
    expect(messages).toEqual(['confirm: Delete all reports?']);
  });

  test('dismissing a confirm is what the default behaviour would have done', async ({ page }) => {
    page.on('dialog', (dialog) => dialog.dismiss());

    await page.getByTestId('trigger-confirm').click();

    await expect(page.getByTestId('dialog-result')).toHaveText('confirm dismissed');
  });

  test('a prompt can be answered with text', async ({ page }) => {
    page.on('dialog', async (dialog) => {
      expect(dialog.defaultValue()).toBe('untitled');
      await dialog.accept('quarterly numbers');
    });

    await page.getByTestId('trigger-prompt').click();

    await expect(page.getByTestId('dialog-result')).toHaveText('prompt returned: quarterly numbers');
  });
});
