import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { MAX_PARALLEL_WORKERS } from '../config/parallelSettings.ts';
import { getSettings } from '../config/settings.ts';
import { poolUsers } from '../users/userCatalog.ts';

/**
 * Writes Allure's environment.properties file into the allure-results directory so the report's
 * "Environment" tab shows what this run actually exercised. Called from the default (coordinator-
 * scoped) `BeforeAll` hook - the one that runs exactly once for the whole run, before any worker
 * thread is even spawned - not the worker-scoped one, mirroring the C# siblings' `[BeforeTestRun]`.
 */
export function writeEnvironmentProperties(): void {
  const settings = getSettings();
  const resultsDir = path.resolve(process.cwd(), 'allure-results');
  mkdirSync(resultsDir, { recursive: true });

  const isCi = process.env.CI === 'true';

  const lines = [
    `BaseUrl=${settings.baseUrl}`,
    `Browser=${settings.browser}`,
    `Headless=${settings.headless}`,
    `Workers=${MAX_PARALLEL_WORKERS}`,
    `UserPoolSize=${poolUsers.length}`,
    `NodeVersion=${process.version}`,
    `OS=${process.platform} ${process.arch}`,
    `CI=${isCi}`,
    `GITHUB_RUN_ID=${process.env.GITHUB_RUN_ID ?? ''}`
  ];

  writeFileSync(path.join(resultsDir, 'environment.properties'), `${lines.join('\n')}\n`);
}
