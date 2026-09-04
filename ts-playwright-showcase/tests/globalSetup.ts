import { copyFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const frameworkRoot = path.resolve(__dirname, '..');

/**
 * Runs once in the coordinator before any worker starts: writes Allure's environment.properties and
 * copies categories.json into the results directory, so a CI upload carries the defect classification
 * rather than it only appearing when a human runs scripts/generate-report.sh.
 *
 * Lives under tests/ rather than a src/ folder because this framework has no src/ - it is a catalog of
 * specs against a fixture app, with no page objects or domain code to hold.
 */
export default function globalSetup(): void {
  const resultsDir = path.join(frameworkRoot, 'allure-results');
  mkdirSync(resultsDir, { recursive: true });

  const lines = [
    `FixturesPort=${process.env.FIXTURES_PORT ?? '8100'}`,
    'Target=fixtures-app (in-repo, started by playwright.config.ts webServer)',
    `NodeVersion=${process.version}`,
    `OS=${process.platform} ${process.arch}`,
    `CI=${process.env.CI === 'true'}`,
    `GITHUB_RUN_ID=${process.env.GITHUB_RUN_ID ?? ''}`
  ];

  writeFileSync(path.join(resultsDir, 'environment.properties'), `${lines.join('\n')}\n`);

  const categories = path.join(frameworkRoot, 'categories.json');
  if (existsSync(categories)) {
    copyFileSync(categories, path.join(resultsDir, 'categories.json'));
  }
}
