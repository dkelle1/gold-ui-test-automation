// Prints a compact pass/fail summary of a cucumber-js JSON report (one line per scenario, plus the
// first line of each failure's error message) - for CI logs specifically. Piping cucumber-js's own
// terminal output through `tee` was tried first and didn't help: Node buffers non-TTY stdout and can
// drop it entirely on process exit before it reaches the pipe, so the underlying test run can finish
// correctly (and write this JSON file synchronously, which is unaffected) while nothing at all shows up
// in the captured job log. Reading the file back afterwards sidesteps that gap. Always exits 0 - this
// is informational only, never the actual pass/fail gate (that's cucumber-js's own exit code).
import { readFileSync } from 'node:fs';

const reportPath = process.argv[2] ?? 'artifacts/cucumber-report.json';

let features;
try {
  features = JSON.parse(readFileSync(reportPath, 'utf-8'));
} catch (error) {
  console.log(`Could not read/parse ${reportPath}: ${error.message}`);
  process.exit(0);
}

let total = 0;
let failed = 0;

for (const feature of features) {
  for (const element of feature.elements ?? []) {
    if (element.type !== 'scenario') continue;
    total += 1;

    const failedStep = (element.steps ?? []).find((step) => step.result?.status === 'failed');
    if (!failedStep) continue;

    failed += 1;
    console.log(`FAILED: ${element.name}  (${feature.uri ?? feature.name ?? '?'})`);
    console.log(`  ${failedStep.keyword ?? ''}${failedStep.name ?? ''}`);
    const firstErrorLine = (failedStep.result.error_message ?? '').split('\n')[0];
    console.log(`  ${firstErrorLine}`);
  }
}

console.log(`\n${total} scenarios, ${failed} failed, ${total - failed} passed`);
