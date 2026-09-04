import { devices, expect, test } from '@playwright/test';

/**
 * Roadmap item: **Mobile Emulation**.
 *
 * What this is and is not, stated plainly because it is routinely oversold: emulation sets the viewport,
 * device pixel ratio, user agent and touch support. It does **not** give you the device's real browser
 * engine, its GPU, its memory pressure, its network stack or its actual font rendering. It catches
 * responsive-layout regressions cheaply and continuously; it does not replace testing on hardware, and a
 * suite that only emulates should not claim mobile coverage.
 *
 * Applied with `test.use({ ...devices[...] })` at describe level rather than as extra projects in
 * playwright.config.ts, so the emulation is visible right next to the tests that depend on it.
 */
test.describe('Desktop layout', () => {
  test.use({ viewport: { width: 1280, height: 720 } });

  test('shows the full navigation and reports no touch support', async ({ page }) => {
    await page.goto('/responsive.html');

    await expect(page.getByTestId('desktop-nav')).toBeVisible();
    await expect(page.getByTestId('mobile-nav')).toBeHidden();
    await expect(page.getByTestId('touch-support')).toHaveText('no');
  });
});

/**
 * The device descriptor's emulation options, listed one by one rather than spread wholesale.
 *
 * `test.use({ ...devices['Pixel 7'] })` inside a `describe` is rejected outright - "Cannot
 * use({ defaultBrowserType }) in a describe group, because it forces a new worker". The descriptor
 * carries `defaultBrowserType` alongside the emulation options, and that one is worker-scoped.
 * Naming the fields explicitly sidesteps it, and doubles as the answer to "what does emulation
 * actually change?" - which is the point of this file.
 */
const pixel7 = devices['Pixel 7'];

test.describe('Pixel 7 emulation', () => {
  // Firefox does not support the isMobile option that this descriptor sets, so the whole describe is
  // skipped there rather than left to fail confusingly in the nightly matrix.
  test.skip(({ browserName }) => browserName === 'firefox', 'isMobile is not supported in Firefox');

  // No `screen`: the descriptor carries one at runtime, but it is neither on Playwright's
  // `DeviceDescriptor` type nor a valid `use` option in 1.56 - so it type-errors even though the
  // runtime quietly ignores it. Listing the fields explicitly is what surfaced that.
  test.use({
    userAgent: pixel7.userAgent,
    viewport: pixel7.viewport,
    deviceScaleFactor: pixel7.deviceScaleFactor,
    isMobile: pixel7.isMobile,
    hasTouch: pixel7.hasTouch
  });

  test('swaps to the compact navigation at a phone viewport', async ({ page }) => {
    await page.goto('/responsive.html');

    await expect(page.getByTestId('mobile-nav')).toBeVisible();
    await expect(page.getByTestId('desktop-nav')).toBeHidden();
  });

  test('emulates touch, pixel ratio and the user agent, not just the width', async ({ page }) => {
    await page.goto('/responsive.html');

    await expect(page.getByTestId('viewport-width')).toHaveText('412');
    await expect(page.getByTestId('touch-support')).toHaveText('yes');
    await expect(page.getByTestId('pixel-ratio')).toHaveText('2.625');
    await expect(page.getByTestId('ua-mobile')).toHaveText('yes');
  });

  test('taps work as touch input rather than mouse clicks', async ({ page }) => {
    await page.goto('/dialogs.html');

    // `tap()` requires hasTouch, which the device descriptor enables - it throws without it, which is a
    // useful signal that the emulation is genuinely applied and not just a resized window.
    page.on('dialog', (dialog) => dialog.accept());
    await page.getByTestId('trigger-confirm').tap();

    await expect(page.getByTestId('dialog-result')).toHaveText('confirm accepted');
  });
});
