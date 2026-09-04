import path from 'node:path';
import { expect, test } from '@playwright/test';

/**
 * Roadmap item: **Downloads**.
 *
 * The ordering trap is the whole lesson here. `page.waitForEvent('download')` has to be *started before*
 * the click that triggers it - awaiting the click first can lose the event, because the download may
 * already have fired by the time the wait begins. Hence the promise-then-click-then-await shape below,
 * which reads oddly the first time and is correct every time.
 *
 * Playwright streams a download to a temporary location and deletes it when the context closes, so a
 * test that wants the bytes must either read the stream or `saveAs()` it somewhere that outlives the run.
 */
test.describe('Downloads', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/downloads.html');
  });

  test('a Content-Disposition attachment raises a download event', async ({ page }) => {
    const downloadPromise = page.waitForEvent('download');
    await page.getByTestId('download-csv').click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toBe('report.csv');
    expect(download.url()).toContain('/download/report.csv');
  });

  test('the downloaded bytes can be read and asserted on', async ({ page }) => {
    const downloadPromise = page.waitForEvent('download');
    await page.getByTestId('download-csv').click();
    const download = await downloadPromise;

    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream) {
      chunks.push(Buffer.from(chunk));
    }
    const csv = Buffer.concat(chunks).toString('utf-8');

    expect(csv.split('\n')[0]).toBe('id,name,price');
    expect(csv).toContain('Quantum Widget');
  });

  test('a blob-generated download is saved to a chosen path', async ({ page }, testInfo) => {
    const downloadPromise = page.waitForEvent('download');
    await page.getByTestId('download-generated').click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toBe('generated.csv');

    // testInfo.outputPath keeps the file inside this test's own output directory, so parallel workers
    // never collide and the artifact is attached to the right test in the report.
    const target = testInfo.outputPath('generated.csv');
    await download.saveAs(target);

    await testInfo.attach('generated.csv', { path: target, contentType: 'text/csv' });
    expect(path.basename(target)).toBe('generated.csv');
  });
});
