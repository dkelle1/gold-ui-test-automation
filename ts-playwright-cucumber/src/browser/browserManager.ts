import { chromium, firefox, webkit } from 'playwright';
import type { Browser, BrowserContext, BrowserType, Page } from 'playwright';
import { getSettings } from '../config/settings.ts';

/**
 * One Playwright browser per WORKER, not per scenario - a deliberate departure from both C# siblings,
 * which each launch a whole new browser per scenario. Cucumber.js's `--parallel` workers are Node
 * `worker_threads`: each one is its own isolated V8 context (a separate module scope, a separate copy
 * of every module-level variable including this one), so a `Browser` instance created here is never
 * touched by more than the one thread that created it - there is no cross-thread sharing risk the way
 * there would be if this project's parallelism worked by running several threads inside one shared V8
 * context. Given that, sharing one browser across every scenario a worker runs (and giving each
 * scenario its own isolated `BrowserContext`/`Page`) is both safe and the standard, recommended
 * Playwright pattern - the one the C# ports specifically avoided because their runtimes' parallelism
 * model made it unsafe there.
 */
let sharedBrowser: Browser | undefined;

function selectBrowserType(): BrowserType {
  const { browser } = getSettings();
  switch (browser) {
    case 'chromium':
      return chromium;
    case 'firefox':
      return firefox;
    case 'webkit':
      return webkit;
    default: {
      const exhaustive: never = browser;
      throw new Error(`Unsupported browser: ${String(exhaustive)}`);
    }
  }
}

function buildLaunchArgs(): string[] {
  const settings = getSettings();
  if (settings.browser !== 'chromium') {
    return [];
  }

  // Required to launch Chromium at all in most CI containers (no --cap-add=SYS_ADMIN, no /dev/shm sized
  // for a real browser) - the same reason both C# siblings' factories always apply these for
  // Chromium/Chrome/Edge. Firefox/WebKit have no equivalent requirement, hence this is Chromium-only.
  const args = ['--no-sandbox', '--disable-dev-shm-usage'];

  if (!settings.headless) {
    // Paired with `viewport: null` in createScenarioSession - this is what actually produces a real
    // maximized window for local, non-headless debugging.
    args.push('--start-maximized');
  }

  return args;
}

/**
 * Launches this worker's one browser. Call exactly once, from a `BeforeAll({ on: HookTarget.WORKER })`
 * hook (see hooks.ts) - that scoping is what makes "once per worker" true instead of "once total".
 */
export async function launchWorkerBrowser(): Promise<void> {
  const settings = getSettings();
  const browserType = selectBrowserType();

  sharedBrowser = settings.remoteUrl
    ? await browserType.connect(settings.remoteUrl)
    : await browserType.launch({ headless: settings.headless, args: buildLaunchArgs() });
}

/** Closes this worker's browser. Call from an `AfterAll({ on: HookTarget.WORKER })` hook. */
export async function closeWorkerBrowser(): Promise<void> {
  await sharedBrowser?.close();
  sharedBrowser = undefined;
}

export interface ScenarioSession {
  context: BrowserContext;
  page: Page;
  /** Console messages captured over the page's lifetime so far - best-effort diagnostic data only. */
  consoleLog: string[];
}

/**
 * A fresh, fully isolated context + page for one scenario, carved out of the worker's shared browser.
 * A plain (non-persistent) `BrowserContext` is isolated by construction - cookies, local storage,
 * cache - so unlike the Selenium sibling's temp `--user-data-dir`, there is no profile directory to
 * create or clean up here at all.
 */
export async function createScenarioSession(): Promise<ScenarioSession> {
  if (!sharedBrowser) {
    throw new Error(
      'launchWorkerBrowser() must run (via a WORKER-scoped BeforeAll hook) before any scenario starts.'
    );
  }

  const settings = getSettings();
  const context = await sharedBrowser.newContext({
    // Headless needs a fixed viewport set explicitly - there's no real window to size otherwise. Headed
    // mode opts out via `viewport: null` so --start-maximized (above) governs the size instead, for
    // local debugging convenience. Playwright never resizes a native OS window at all here either - it
    // sets the viewport directly - so the headless-virtual-screen trap documented in the Selenium
    // sibling's README structurally cannot happen in this framework, same as in the C# Playwright one.
    viewport: settings.headless ? { width: 1920, height: 1080 } : null
  });

  context.setDefaultTimeout(settings.explicitWaitMs);
  context.setDefaultNavigationTimeout(settings.pageLoadTimeoutMs);

  const page = await context.newPage();
  const consoleLog: string[] = [];
  page.on('console', (msg) => consoleLog.push(`[${msg.type()}] ${msg.text()}`));

  await page.goto(settings.baseUrl);

  return { context, page, consoleLog };
}
