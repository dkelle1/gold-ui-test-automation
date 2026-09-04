import { expect, test } from '@playwright/test';

/**
 * Roadmap items: **Network Interception** and **API Mocking**.
 *
 * The highest-value pair on this page, and the reason the fixture app exists at all. saucedemo.com bakes
 * its product list into its bundle: there is no inventory request to observe, no failure response to
 * inject, and no empty state that can ever be reached through the UI. So the four sibling frameworks
 * cannot test the catalog's error path, empty path or slow path - not because of a gap in their design,
 * but because those states are unreachable from the outside.
 *
 * Mocking removes that limit. Every state below is a real UI path with real assertions, reached in
 * milliseconds and deterministically, with no backend to arrange.
 */
test.describe('Network interception', () => {
  test('the real request can be observed and asserted on', async ({ page }) => {
    const requestPromise = page.waitForRequest('**/api/products');
    await page.goto('/catalog.html');
    const request = await requestPromise;

    expect(request.method()).toBe('GET');

    const response = await request.response();
    expect(response?.status()).toBe(200);

    await expect(page.getByTestId('catalog-item')).toHaveCount(3);
  });

  test('a route can be blocked outright', async ({ page }) => {
    await page.route('**/api/products', (route) => route.abort());

    await page.goto('/catalog.html');

    // The page's own catch branch - a path no amount of UI driving could otherwise reach.
    await expect(page.getByTestId('catalog-error')).toBeVisible();
  });
});

test.describe('API mocking', () => {
  test('an empty catalog renders the empty state', async ({ page }) => {
    await page.route('**/api/products', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    );

    await page.goto('/catalog.html');

    await expect(page.getByTestId('catalog-empty')).toBeVisible();
    await expect(page.getByTestId('catalog-item')).toHaveCount(0);
  });

  test('a 500 renders the error state', async ({ page }) => {
    await page.route('**/api/products', (route) =>
      route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"boom"}' })
    );

    await page.goto('/catalog.html');

    await expect(page.getByTestId('catalog-error')).toBeVisible();
    await expect(page.getByTestId('catalog-loading')).toBeHidden();
  });

  test('a slow response leaves the loading state visible', async ({ page }) => {
    await page.route('**/api/products', async (route) => {
      // Deterministic latency, injected client-side - no throttling profile and no real slow network.
      await new Promise((resolve) => setTimeout(resolve, 1500));
      await route.continue();
    });

    await page.goto('/catalog.html', { waitUntil: 'commit' });

    await expect(page.getByTestId('catalog-loading')).toBeVisible();
    // And it still resolves correctly once the delay elapses.
    await expect(page.getByTestId('catalog-item')).toHaveCount(3);
  });

  test('a response can be rewritten while still hitting the real endpoint', async ({ page }) => {
    // route.fetch() performs the real request, so the mock stays anchored to the actual API's shape
    // instead of drifting into a fiction the backend never returns.
    await page.route('**/api/products', async (route) => {
      const response = await route.fetch();
      const products = (await response.json()) as { id: number; name: string; price: number }[];

      await route.fulfill({
        response,
        json: products.map((p) => ({ ...p, name: p.name.toUpperCase() }))
      });
    });

    await page.goto('/catalog.html');

    await expect(page.getByTestId('catalog-item')).toHaveCount(3);
    await expect(page.getByTestId('catalog-item').first()).toContainText('QUANTUM WIDGET');
  });
});
