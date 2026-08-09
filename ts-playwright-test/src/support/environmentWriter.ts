import { copyFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { MAX_PARALLEL_WORKERS } from '../config/parallelSettings';
import { getSettings } from '../config/settings';
import { poolUsers } from '../users/userCatalog';

const frameworkRoot = path.resolve(__dirname, '..', '..');

/**
 * Writes Allure's environment.properties so the report's "Environment" tab shows what this run actually
 * exercised, and copies categories.json in alongside it so the defect classification is present in the
 * results directory that CI uploads - rather than only when a human runs scripts/generate-report.sh.
 *
 * Wired up as Playwright's `globalSetup`, which runs once in the coordinator process before any worker
 * starts. The direct equivalent of the C# siblings' `[BeforeTestRun]` and of the Cucumber sibling's
 * untargeted `BeforeAll`.
 */
export default function globalSetup(): void {
  const settings = getSettings();
  const resultsDir = path.join(frameworkRoot, 'allure-results');
  mkdirSync(resultsDir, { recursive: true });

  const isCi = process.env.CI === 'true';

  const lines = [
    `BaseUrl=${settings.baseUrl}`,
    `Headless=${settings.headless}`,
    `Workers=${MAX_PARALLEL_WORKERS}`,
    `UserPoolSize=${poolUsers.length}`,
    // No Browser= line, unlike every sibling framework: which engines a run used is per-test here, not
    // per-run, and both reporters already record it as the project name on each result.
    `NodeVersion=${process.version}`,
    `OS=${process.platform} ${process.arch}`,
    `CI=${isCi}`,
    `GITHUB_RUN_ID=${process.env.GITHUB_RUN_ID ?? ''}`
  ];

  writeFileSync(path.join(resultsDir, 'environment.properties'), `${lines.join('\n')}\n`);

  const categories = path.join(frameworkRoot, 'categories.json');
  if (existsSync(categories)) {
    copyFileSync(categories, path.join(resultsDir, 'categories.json'));
  }
}
