# ts-playwright-test

TypeScript UI test automation for [saucedemo.com](https://www.saucedemo.com/), built on **Playwright
Test** - Playwright's own runner, with no BDD layer.

This is the deliberate counterpart to [`../ts-playwright-cucumber/`](../ts-playwright-cucumber/): same
language, same browser driver, same application, the same twelve scenarios, opposite philosophy. One
drives Playwright through Cucumber.js and Gherkin; this one uses the runner Playwright ships. The
[side-by-side comparison](#how-this-differs-from-the-cucumberjs-sibling) is the main reason this
framework exists.

## Stack

| Concern            | Choice                                            |
| ------------------ | ------------------------------------------------- |
| Language           | TypeScript 6 (strict, `noUncheckedIndexedAccess`) |
| Browser automation | Playwright 1.56.1 (Chromium / Firefox / WebKit)   |
| Runner             | Playwright Test (`fullyParallel`, 3 workers)      |
| Spec format        | TypeScript test files - no Gherkin                |
| Test data          | `@faker-js/faker`                                 |
| Reporting          | Allure + Playwright HTML + JSON                   |
| CI                 | GitHub Actions (primary) + `Jenkinsfile`          |

## Prerequisites

- Node.js >= 22
- `npm ci` then `npx playwright install --with-deps chromium` (add `firefox webkit` for the full matrix)

The Allure CLI is optional and only needed for `scripts/generate-report.sh`. The Playwright HTML report
needs nothing beyond what `npm ci` already installs.

## Running locally

```bash
npm ci
npx playwright install --with-deps chromium

npm test                 # chromium, all 12 tests
npm run test:smoke       # chromium, @smoke only
npm run test:all-browsers  # chromium + firefox + webkit
npm run test:ui          # Playwright UI mode - watch, time-travel, pick locators
npm run test:unit        # user-assignment unit tests, no browser

npm run report           # open the Playwright HTML report from the last run
```

Or via the wrapper scripts, which mirror the sibling frameworks':

```bash
scripts/run-tests.sh                 # chromium, all tests
scripts/run-tests.sh chromium @smoke # chromium, @smoke only
scripts/run-tests.sh all             # every engine
scripts/generate-report.sh           # Allure report (needs the allure CLI)
```

Debugging, none of which the Cucumber sibling can offer:

```bash
npx playwright test --ui                    # UI mode
npx playwright test --debug                 # step through with Inspector
npx playwright test --last-failed           # re-run only what failed
npx playwright show-trace test-results/**/trace.zip
PWDEBUG=1 npx playwright test --project=chromium --grep @smoke
```

## Running in CI

- **GitHub Actions**: [`.github/workflows/ts-playwright-test.yml`](../.github/workflows/ts-playwright-test.yml).
  Path-filtered to this folder. Pull requests run **chromium only** for fast feedback; the nightly
  canary at 07:00 UTC runs the **full three-engine matrix**. `workflow_dispatch` takes a `project`
  choice and a `grep_filter`.
- **Jenkins**: [`Jenkinsfile`](Jenkinsfile) - runs in `mcr.microsoft.com/playwright:v1.56.1-noble`,
  which already ships all three engines, so no install step is needed there.

Both publish Allure results; the Actions workflow also publishes the report to GitHub Pages under
`ts-playwright-test/<run number>/` with trend history, and uploads the Playwright HTML report and any
traces as artifacts.

## Configuration

Layered, highest precedence last:

1. [`appsettings.json`](appsettings.json) - local defaults
2. [`appsettings.ci.json`](appsettings.ci.json) - merged over the base whenever `CI=true`
3. Environment variables: `BASE_URL`, `HEADLESS`, `REMOTE_URL`, `EXPLICIT_WAIT_MS`, `PAGE_LOAD_TIMEOUT_MS`

Same scheme as all four sibling frameworks, with **one field deliberately missing: `Browser`**. Which
engine a run uses is a Playwright _project_, selected with `--project=firefox`, not a config key this
code reads and maps onto a browser type by hand. `TestSettings` shrank accordingly - see
[`src/config/settings.ts`](src/config/settings.ts).

## Project structure

```
playwright.config.ts       projects, reporters, timeouts, retries, trace/screenshot/video policy
src/
  fixtures.ts              the heart of this framework: every fixture and test option
  config/                  settings loader + MAX_PARALLEL_WORKERS
  users/                   account catalog + parallelIndex -> account assignment
  pages/                   page objects, exposing Locators for the tests to assert on
  testdata/                Faker-backed factories + fixed product reference data
  support/                 globalSetup (Allure environment.properties + categories.json)
tests/
  login.spec.ts            \
  cart.spec.ts              >  the same twelve scenarios as the sibling frameworks' .feature files
  checkout.spec.ts         /
  unit/assignedUser.test.ts  plain Node test-runner unit tests, no browser
```

## Parallel execution & user assignment

saucedemo has three accounts that can complete a checkout, so the suite runs three workers and pins one
account to each for that worker's lifetime.

```ts
assignedUser: [
  async ({}, use, workerInfo) => {
    await use(getAssignedUser(workerInfo.parallelIndex));
  },
  { scope: 'worker' }
],
```

Three things fall out of Playwright owning this that are worth calling out:

- **`parallelIndex` is a guaranteed stable integer in `[0, workers)`** for a worker's whole life, so
  `getAssignedUser` is a pure function of it. The Cucumber sibling has to read
  `process.env.CUCUMBER_WORKER_ID` and handle it being unset; the two C# siblings need a real
  `BlockingCollection` lease pool because their runtimes cannot pin an account to a worker at all.
- **The browser matrix costs no extra accounts.** `parallelIndex` is bounded by worker count, not by
  project count, and two tests sharing an index never run concurrently - so `--project=all` still needs
  only three accounts.
- **The worker count cannot drift out of range.** `playwright.config.ts` imports `MAX_PARALLEL_WORKERS`
  and `assignedUser.ts` throws at import time if the pool is smaller. The Cucumber sibling has to repeat
  the number as a literal in `cucumber.js` (Cucumber reads that file before any TypeScript loader
  exists) and its CI workflow needs a dedicated guard step to reject an over-provisioned run.

A test that must use one specific account opts in by name:

```ts
test.describe('problem_user', () => {
  test.use({ userOverride: 'problem_user' });
  ...
});
```

## Reporting

Three reporters, on every run:

| Reporter        | Output                             | Why keep it                                                                       |
| --------------- | ---------------------------------- | --------------------------------------------------------------------------------- |
| Allure          | `allure-results/`                  | cross-framework comparability with the four siblings; history and trends on Pages |
| Playwright HTML | `playwright-report/`               | zero setup, no JDK, no CLI; embeds traces, videos and screenshots inline          |
| JSON            | `artifacts/playwright-report.json` | machine-readable summary for CI                                                   |

Failure evidence is configuration, not code: `screenshot: 'only-on-failure'`,
`video: 'retain-on-failure'`, `trace: 'on-first-retry'`. The sibling's hand-written
`attachFailureEvidence()` - which captures URL, screenshot, page source and console log in a careful
best-effort order because the page may already be dead - is replaced entirely, except for the browser
console log, which is still worth one small auto-fixture in `fixtures.ts`.

Allure gets `user` and `parallelIndex` as parameters on every test that uses an account, and Playwright
derives its step tree from the actions themselves (`Fill getByTestId('username')`, `Click …`) with no
manual instrumentation.

## How this differs from the Cucumber.js sibling

Same 12 scenarios, same page objects, same user pool, same demo site. **28 source files and 1,165 lines
become 18 files and 738 lines** - and none of the removed code was coverage.

| Concern                  | `ts-playwright-cucumber`                                             | `ts-playwright-test`                  |
| ------------------------ | -------------------------------------------------------------------- | ------------------------------------- |
| Spec format              | Gherkin `.feature` + step definitions                                | `*.spec.ts`                           |
| Per-scenario context     | `world.ts` (`SauceDemoWorld`)                                        | fixtures                              |
| Setup/teardown           | `hooks.ts` with `Before`/`After`/`BeforeAll`                         | fixtures, incl. `auto` ones           |
| `Background:`            | Gherkin block re-running steps                                       | the `loggedIn` fixture                |
| `Scenario Outline`       | `Examples` table + `<generated>` sentinel string                     | a loop over typed data                |
| Specific account         | `@user:x` tag parsed in `tagHelpers.ts`                              | `test.use({ userOverride: 'x' })`     |
| Worker → account         | `process.env.CUCUMBER_WORKER_ID`                                     | `workerInfo.parallelIndex`            |
| Waiting                  | `basePage.ts` wait helpers + `retryUntil.ts`                         | web-first assertions + `expect.poll`  |
| Browser lifecycle        | `browserManager.ts`, launch-per-worker by hand                       | the runner's                          |
| Browser matrix           | one engine per run, from config                                      | `projects`                            |
| Retries / flake evidence | none                                                                 | `retries` + `trace: 'on-first-retry'` |
| Failure evidence         | `allureAttachments.ts` + `screenshotHelper.ts`                       | config flags                          |
| TypeScript loading       | ~30 lines of doc comment on tsx, worker threads and `.ts` extensions | nothing to explain                    |
| Worker count             | literal duplicated in `cucumber.js` + a CI guard step                | one imported constant                 |

Deleted outright, with no replacement written: `world.ts`, `hooks.ts`, `browser/browserManager.ts`,
`support/retryUntil.ts`, `support/screenshotHelper.ts`, `support/allureAttachments.ts`,
`users/tagHelpers.ts`, and all four `steps/*.ts`.

### What the sibling still does better

This is not a one-sided comparison, and the gallery would be less useful if it pretended otherwise:

- **A non-programmer can read and edit a `.feature` file.** The `Examples` table is genuinely editable
  by a product owner or manual tester. `invalidLoginCases` in `login.spec.ts` is not.
- **Gherkin is a shared vocabulary.** "Given I log in with my assigned user" survives a rewrite of the
  automation underneath it; a fixture name does not have the same standing in a conversation.
- **Step reuse is enforced by the runner.** Cucumber refuses to run an ambiguous step, which pushes a
  team towards one canonical phrasing per action. Nothing here stops two tests doing the same thing two
  different ways.

The honest summary: pick the sibling if the Gherkin layer is _read by someone who does not write code_.
If it is written and read only by the same engineers who write the automation, that layer is cost
without benefit, and this framework is the cheaper shape.

### Locators

The one behavioural change from the siblings' page objects. They locate a product's button with an
interpolated XPath:

```
//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='<name>']]//button[text()='Add to cart']
```

This framework composes locators instead:

```ts
this.page
  .locator('.inventory_item')
  .filter({ has: this.page.getByText(productName, { exact: true }) })
  .getByRole('button', { name: 'Add to cart', exact: true });
```

Interpolating a product name into an XPath predicate breaks on any name containing a quote, and
`//button[text()='…']` is a structural match that never asserts the element is a button. `getByRole`
matches the accessible role - what a user perceives, and what survives markup changes that leave the
role intact. `exact: true` is set because `getByRole`'s name matching is substring-based by default; the
XPath it replaces was an exact match, so this keeps it equivalent rather than looser.

Everywhere else the `data-test` attributes are unchanged - `testIdAttribute: 'data-test'` in
`playwright.config.ts` is what turns `[data-test='username']` into `getByTestId('username')` once,
globally.

## Pinning the Playwright version

`@playwright/test` is pinned to an exact version (`1.56.1`, no caret). Playwright's npm package and its
browser binaries are a matched pair: a floating range can pull a build whose bundled engine revision is
not the one CI cached or the one the Docker image ships. The `Jenkinsfile`'s
`mcr.microsoft.com/playwright:v1.56.1-noble` tag must be bumped together with this dependency.

## Known limitations

- **The Firefox and WebKit projects are configured but not yet exercised.** They are correct as written
  and installed by CI, but nothing has run green on them yet - the first nightly matrix run is what will
  confirm them. Only Chromium has actually executed this suite.
- **This suite has not yet run against the live saucedemo.com.** It was developed in a sandbox whose
  network policy blocks that host (403 at the proxy on `CONNECT`), so it was verified end-to-end against
  a local stand-in reproducing the DOM facts the sibling frameworks document as verified against the
  real site: the `data-test` attribute names, the `.inventory_item` / `.cart_item` classes, the literal
  `Add to cart` / `Remove` button text, and the exact error-banner strings. That proves the framework
  mechanics - fixtures, page objects, locator composition, assertions, parallel account assignment, all
  three reporters - and does **not** prove the markup assumptions still hold on the live site. The first
  CI run is the real verification.
- **saucedemo.com degrades under repeated automated traffic.** Inherited, well-documented behaviour: see
  the Selenium sibling's README. `InventoryPage.toggle()` keeps a click-and-confirm retry loop for this
  reason. It is the only hand-rolled retry left in the framework, and it survives because it re-issues an
  _action_ - `expect().toBeVisible()` re-checks, it does not re-click.
- **`problem_user`'s broken checkout is a real product defect**, asserted deliberately and tagged
  `@known-issue`. If saucedemo ever fixes it, that test fails - which is the point.
- **The three invalid-credential tests record no `user` Allure parameter.** They never request the
  `activeUser` fixture, because they log in with fabricated credentials that match no account. Correct
  behaviour, but it looks like a gap in the report until you know why.
- **`moduleResolution` is `Bundler`, not `Node16`.** `@faker-js/faker` v10 is ESM-only; under Node16
  resolution TypeScript rejects importing it from a CommonJS file (TS1479). That diagnostic describes
  older Node - this project requires Node >= 22, where `require(esm)` is on by default and the import
  resolves. `Bundler` also happens to be the accurate description of how Playwright loads these files.
  See the comment in [`tsconfig.json`](tsconfig.json) before changing it back.

## Adding a test / a page object

1. **New page object**: extend `BasePage`, build `Locator`s in the constructor, expose the ones tests
   assert on as `readonly` public fields and keep the rest private. Return the next page object from any
   method that navigates.
2. **New fixture**: add it to `SauceDemoFixtures` in [`src/fixtures.ts`](src/fixtures.ts) and implement
   it there. Use `{ scope: 'worker' }` for anything expensive that a whole worker can share, and
   `{ auto: true }` only for things every test genuinely needs.
3. **New test**: put it in the matching `tests/*.spec.ts`, tag it (`{ tag: ['@smoke'] }`) so `--grep`
   selection keeps working, and assert with web-first assertions on Locators rather than reading values
   out into variables first.
4. Run `npm run typecheck && npm run lint && npm run format` before pushing - CI runs all three.

## License

[MIT](../LICENSE)
