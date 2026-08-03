import { After, AfterAll, Before, BeforeAll, Status, setDefaultTimeout } from '@cucumber/cucumber';
import { AllureCucumberTestRuntime } from 'allure-cucumberjs';
import * as allure from 'allure-js-commons';
import { setGlobalTestRuntime } from 'allure-js-commons/sdk/runtime';
import { createRequire } from 'node:module';
import { closeWorkerBrowser, createScenarioSession, launchWorkerBrowser } from './browser/browserManager.ts';
import { attachFailureEvidence } from './support/allureAttachments.ts';
import { writeEnvironmentProperties } from './support/environmentWriter.ts';
import { getAssignedUser } from './users/assignedUser.ts';
import { getTaggedUser } from './users/tagHelpers.ts';
import type { SauceDemoWorld } from './world.ts';
import type { HookTarget as HookTargetEnum } from '@cucumber/cucumber';

/**
 * @cucumber/cucumber@13.2.0's ESM entry point (lib/wrapper.mjs) hand-maintains its own re-export list
 * and simply omits `HookTarget` from it - confirmed directly (`Object.keys(require('@cucumber/cucumber'))`
 * includes it on the CJS build; the ESM wrapper's export list does not), so `import { HookTarget }
 * from '@cucumber/cucumber'` throws a SyntaxError at load time despite being exactly the documented,
 * correctly-typed API. Pulling the runtime value through the CJS build via `createRequire` sidesteps the
 * gap; the `import type` above keeps it nominally typed as the real enum rather than `any`.
 */
const HookTarget = createRequire(import.meta.url)('@cucumber/cucumber').HookTarget as {
  WORKER: HookTargetEnum.WORKER;
  COORDINATOR: HookTargetEnum.COORDINATOR;
};

// Cucumber.js's own step-execution timeout (Given/When/Then), separate from Playwright's per-action
// timeout configured in browserManager.ts - generous because performance_glitch_user's ~5s artificial
// delays plus several awaited Playwright actions in one step can otherwise trip the 5s library default.
setDefaultTimeout(60_000);

/**
 * Runs exactly once for the whole run, before any worker thread is spawned - the default target for
 * BeforeAll (no `on` option), and the direct equivalent of the C# siblings' [BeforeTestRun].
 */
BeforeAll(async function () {
  writeEnvironmentProperties();
});

/**
 * allure-cucumberjs's own reporter (registered via cucumber.js's `format` array) sets up the Allure
 * runtime with an untargeted `BeforeAll` - i.e. once, on the coordinator only (see node_modules/allure-
 * cucumberjs/dist/esm/index.js). That runtime is a plain module-level variable inside allure-js-commons,
 * and - exactly like every other untargeted BeforeAll - never reaches any worker thread's own isolated
 * copy of that module. Every `allure.parameter()`/`allure.attachment()` call below runs from inside a
 * worker (that's the only place scenarios ever execute), so without this, every one of those calls
 * silently no-ops against the fallback "Noop" runtime, printing "no test runtime is found. Please check
 * test framework configuration" instead of actually recording anything - confirmed directly: this is
 * real CI output, not a guess, and disappeared once this hook was added. Mirrors allure-cucumberjs's own
 * registration line for line, just re-scoped to run inside each worker too.
 */
BeforeAll({ on: HookTarget.WORKER }, function () {
  setGlobalTestRuntime(new AllureCucumberTestRuntime());
});

/**
 * Runs once per worker THREAD - not once per scenario like both C# siblings, and not once total like
 * the untargeted BeforeAll above. See browserManager.ts's doc comment for why launching one browser per
 * worker (rather than per scenario) is safe under Cucumber.js's parallel model specifically.
 */
BeforeAll({ on: HookTarget.WORKER }, async function () {
  await launchWorkerBrowser();
});

AfterAll({ on: HookTarget.WORKER }, async function () {
  await closeWorkerBrowser();
});

Before(async function (this: SauceDemoWorld, { pickle }) {
  this.userAccount = getTaggedUser(pickle) ?? getAssignedUser();
  await allure.parameter('user', this.userAccount.username);
  await allure.parameter('worker', process.env.CUCUMBER_WORKER_ID ?? 'single-threaded');

  this.session = await createScenarioSession();
});

After(async function (this: SauceDemoWorld, { result }) {
  if (result?.status === Status.FAILED && this.session) {
    await attachFailureEvidence(this.session);
  }

  await this.session?.context.close();
});
