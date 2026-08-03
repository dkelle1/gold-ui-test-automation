import type { Page } from 'playwright';

export function captureScreenshot(page: Page): Promise<Buffer> {
  return page.screenshot();
}
