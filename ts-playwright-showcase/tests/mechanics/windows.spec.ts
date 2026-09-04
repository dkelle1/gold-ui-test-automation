import { expect, test } from '@playwright/test';

/**
 * Roadmap item: **Multiple Tabs**.
 *
 * There is no "switch to window" call here, which is the main difference from Selenium's
 * `driver.switchTo().window(handle)`. A new tab arrives as its own `Page` object, and the original
 * `page` stays exactly as usable as it was - both are live at the same time, and a test drives whichever
 * one it holds.
 *
 * The same start-the-wait-before-the-click ordering as downloads applies, for the same reason.
 */
test.describe('Windows and tabs', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/windows.html');
  });

  test('a target=_blank link opens a page on the context', async ({ context, page }) => {
    const pagePromise = context.waitForEvent('page');
    await page.getByTestId('new-tab-link').click();
    const newTab = await pagePromise;

    await newTab.waitForLoadState();
    await expect(newTab.getByTestId('popup-title')).toHaveText('Popup window');
    await expect(newTab.getByTestId('popup-origin')).toHaveText('opened via: link');

    // The opener is untouched and still driveable - no switching, no handles.
    await expect(page.getByTestId('page-title')).toHaveText('Windows');
    expect(context.pages()).toHaveLength(2);
  });

  test('window.open surfaces as a popup event on the opener', async ({ page }) => {
    // `page.waitForEvent('popup')` is the narrower version of the context-level event: it only fires for
    // pages this page opened, so it cannot accidentally match a tab opened elsewhere in the context.
    const popupPromise = page.waitForEvent('popup');
    await page.getByTestId('open-popup').click();
    const popup = await popupPromise;

    await popup.waitForLoadState();
    await expect(popup.getByTestId('popup-origin')).toHaveText('opened via: script');
    await expect(page.getByTestId('popup-state')).toHaveText('popup opened');
  });

  test('a popup closed by the opener reports as closed', async ({ page }) => {
    const popupPromise = page.waitForEvent('popup');
    await page.getByTestId('open-popup').click();
    const popup = await popupPromise;
    await popup.waitForLoadState();

    await page.getByTestId('close-popup').click();

    await expect(page.getByTestId('popup-state')).toHaveText('popup closed');
    await expect.poll(() => popup.isClosed()).toBe(true);
  });
});
