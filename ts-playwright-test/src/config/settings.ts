import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

/**
 * Bound from `TestSettings` in appsettings*.json, overridable by the env vars named in each field's
 * fallback below.
 *
 * Deliberately smaller than the Cucumber sibling's equivalent: there is no `browser` field here. Which
 * engine a run uses is a Playwright *project* (see playwright.config.ts), selected with
 * `--project=firefox` rather than a config key this code has to read, validate and map onto a
 * `BrowserType` by hand. That is one of the clearer places where the native runner replaces bespoke
 * configuration rather than merely wrapping it.
 */
export interface TestSettings {
  baseUrl: string;
  headless: boolean;
  /** A Playwright browser-server WS endpoint (e.g. `playwright run-server`). Null means launch locally. */
  remoteUrl: string | null;
  explicitWaitMs: number;
  pageLoadTimeoutMs: number;
}

interface RawTestSettings {
  BaseUrl?: string;
  Headless?: boolean;
  RemoteUrl?: string | null;
  ExplicitWaitMs?: number;
  PageLoadTimeoutMs?: number;
}

function readJsonConfig(fileName: string): RawTestSettings {
  const filePath = path.resolve(__dirname, '..', '..', fileName);
  if (!existsSync(filePath)) {
    return {};
  }

  const parsed = JSON.parse(readFileSync(filePath, 'utf-8')) as { TestSettings?: RawTestSettings };
  return parsed.TestSettings ?? {};
}

function loadSettings(): TestSettings {
  const isCi = process.env.CI === 'true';

  const base = readJsonConfig('appsettings.json');
  // appsettings.ci.json overrides base settings whenever CI=true, exactly like every sibling framework -
  // layered in before env vars, which stay the highest-precedence override either way.
  const ciOverrides = isCi ? readJsonConfig('appsettings.ci.json') : {};
  const merged: RawTestSettings = { ...base, ...ciOverrides };

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
    headless: process.env.HEADLESS ? process.env.HEADLESS === 'true' : (merged.Headless ?? false),
    remoteUrl: process.env.REMOTE_URL ?? merged.RemoteUrl ?? null,
    explicitWaitMs: Number(process.env.EXPLICIT_WAIT_MS ?? merged.ExplicitWaitMs ?? 20000),
    pageLoadTimeoutMs: Number(process.env.PAGE_LOAD_TIMEOUT_MS ?? merged.PageLoadTimeoutMs ?? 30000)
  };
}

let cached: TestSettings | undefined;

/**
 * Loaded once and cached for the process lifetime - settings never change mid-run.
 *
 * Note that "the process lifetime" means something different here than in the Cucumber sibling: this is
 * read by playwright.config.ts in the coordinator AND independently re-read in each worker process,
 * because Playwright workers are separate processes that each load the config file afresh. Reading from
 * `__dirname` rather than `process.cwd()` is what keeps that reliable - a worker's cwd is not guaranteed
 * to be the framework folder.
 */
export function getSettings(): TestSettings {
  if (!cached) {
    cached = loadSettings();
  }
  return cached;
}
