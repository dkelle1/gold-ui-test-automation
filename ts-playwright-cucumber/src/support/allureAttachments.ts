import * as allure from 'allure-js-commons';
import type { ScenarioSession } from '../browser/browserManager.ts';
import { captureScreenshot } from './screenshotHelper.ts';

/**
 * Collects whatever failure evidence the browser can still give us. Each attachment is captured
 * independently and best-effort, in ascending order of how much of a live page/browser it needs,
 * because the scenarios that most need evidence include the ones that failed *because* the page or
 * browser died - and there a screenshot/content read throws while `page.url()` (a plain, synchronous,
 * locally-cached read - not even async, unlike every other method here that touches the live page)
 * still answers.
 *
 * Nothing here is allowed to throw: this runs from an `After` hook, and while Cucumber.js (unlike
 * Reqnroll) does not abort later same-type hooks when one throws, a failure here still shouldn't cost
 * the run its actual evidence-gathering attempts for the OTHER attachments below it.
 */
export async function attachFailureEvidence(session: ScenarioSession): Promise<void> {
  await tryAttach('final-url', 'text/plain', () => Promise.resolve(session.page.url()));
  await tryAttach('failure-screenshot', 'image/png', () => captureScreenshot(session.page));
  await tryAttach('page-source', 'text/html', () => session.page.content());

  // Best-effort only, like the other siblings' console-log attachment: unlike Selenium's pull-based
  // Manage().Logs API, the console log here is push-based (browserManager.ts subscribes to the page's
  // 'console' event as soon as the page is created), so there's nothing to catch here beyond "were there
  // any messages at all".
  if (session.consoleLog.length > 0) {
    await tryAttach('browser-console-log', 'text/plain', () =>
      Promise.resolve(session.consoleLog.join('\n'))
    );
  }
}

async function tryAttach(
  name: string,
  mimeType: string,
  capture: () => Promise<string | Buffer>
): Promise<void> {
  try {
    await allure.attachment(name, await capture(), mimeType);
  } catch (error) {
    const reason = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
    console.error(`Could not attach '${name}' failure evidence: ${reason}`);
  }
}
