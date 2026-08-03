import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

export type BrowserName = 'chromium' | 'firefox' | 'webkit';

/** Bound from `TestSettings` in appsettings*.json, overridable by the plain env vars named in each field's fallback below. */
export interface TestSettings {
  baseUrl: string;
  browser: BrowserName;
  headless: boolean;
  /** A Playwright browser-server WS endpoint (e.g. `playwright run-server`). Null means launch a local browser. */
  remoteUrl: string | null;
  explicitWaitMs: number;
  pageLoadTimeoutMs: number;
}

interface RawTestSettings {
  BaseUrl?: string;
  Browser?: string;
  Headless?: boolean;
  RemoteUrl?: string | null;
  ExplicitWaitMs?: number;
  PageLoadTimeoutMs?: number;
}

function readJsonConfig(fileName: string): RawTestSettings {
  const filePath = path.resolve(process.cwd(), fileName);
  if (!existsSync(filePath)) {
    return {};
  }

  const parsed = JSON.parse(readFileSync(filePath, 'utf-8')) as { TestSettings?: RawTestSettings };
  return parsed.TestSettings ?? {};
}

function isBrowserName(value: string): value is BrowserName {
  return value === 'chromium' || value === 'firefox' || value === 'webkit';
}

function loadSettings(): TestSettings {
  const isCi = process.env.CI === 'true';

  const base = readJsonConfig('appsettings.json');
  // appsettings.ci.json overrides base settings whenever CI=true, exactly like the Reqnroll siblings -
  // layered in before env vars, which stay the highest-precedence override either way.
  const ciOverrides = isCi ? readJsonConfig('appsettings.ci.json') : {};
  const merged: RawTestSettings = { ...base, ...ciOverrides };

  const browser = process.env.BROWSER ?? merged.Browser ?? 'chromium';
  if (!isBrowserName(browser)) {
    throw new Error(`Unsupported browser "${browser}" - expected chromium, firefox, or webkit.`);
  }

  const baseUrl = process.env.BASE_URL ?? merged.BaseUrl;
  if (!baseUrl) {
    throw new Error('TestSettings.BaseUrl must be configured (appsettings.json or the BASE_URL env var).');
  }
  try {
    new URL(baseUrl);
  } catch {
    throw new Error(`TestSettings.BaseUrl is not a valid absolute URL: "${baseUrl}".`);
  }

  return {
    baseUrl,
    browser,
    headless: process.env.HEADLESS ? process.env.HEADLESS === 'true' : (merged.Headless ?? false),
    remoteUrl: process.env.REMOTE_URL ?? merged.RemoteUrl ?? null,
    explicitWaitMs: Number(process.env.EXPLICIT_WAIT_MS ?? merged.ExplicitWaitMs ?? 20000),
    pageLoadTimeoutMs: Number(process.env.PAGE_LOAD_TIMEOUT_MS ?? merged.PageLoadTimeoutMs ?? 30000)
  };
}

let cached: TestSettings | undefined;

/** Loaded once and cached for the process lifetime - settings never change mid-run. */
export function getSettings(): TestSettings {
  if (!cached) {
    cached = loadSettings();
  }
  return cached;
}
