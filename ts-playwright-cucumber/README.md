# ts-playwright-cucumber

TypeScript UI test automation for [saucedemo.com](https://www.saucedemo.com/), built with Playwright,
Cucumber.js (Gherkin/BDD), chai and Allure reporting. This is the third framework in this repo's
gallery, and the first not written in C# - see
["How this differs from the C# siblings"](#how-this-differs-from-the-c-siblings) below for what that
actually changes, not just how the syntax looks different.

## Stack

- **TypeScript** on Node.js 22+, run directly via [`tsx`](https://github.com/privatenumber/tsx) (no build step)
- **Playwright** for browser automation (Chromium by default; Firefox and WebKit also supported)
- **Cucumber.js** (`@cucumber/cucumber`) for Gherkin feature files and step bindings
- **Allure** (`allure-cucumberjs`) for HTML test reports
- **chai** for assertions
- **@faker-js/faker** for fake test data (never for login credentials - see [`src/users/userCatalog.ts`](src/users/userCatalog.ts))

## Prerequisites

- Node.js 22+
- Playwright's browser binaries, installed once: `npx playwright install chromium` (swap for `firefox`
  or `webkit`, or drop the argument to install all three, if you plan to run with a different `BROWSER`)

## Running locally

```bash
npm ci
scripts/run-tests.sh              # 3 parallel workers, all scenarios
scripts/run-tests.sh 3 @smoke     # 3 parallel workers, @smoke only
```

(`scripts/run-tests.ps1` is the PowerShell equivalent.) By default this runs headed (a visible browser
window), since `appsettings.json` sets `Headless: false`. After a run:

```bash
scripts/generate-report.sh   # or generate-report.ps1
```

opens the Allure report for the results just produced.

Other useful scripts: `npm run typecheck`, `npm run lint`, `npm run format`, `npm run test:unit` (the
browser-independent unit tests).

## Running in CI

- **GitHub Actions**: [`.github/workflows/ts-playwright-cucumber.yml`](../.github/workflows/ts-playwright-cucumber.yml),
  triggered on push/PR to this folder, `workflow_dispatch` (custom worker count / tag filter), and a
  nightly schedule. Publishes an Allure report to GitHub Pages under `ts-playwright/<run-number>/` and
  `ts-playwright/latest/` (namespaced so it never collides with either C# sibling's own reports on the
  same `gh-pages` branch).
- **Jenkins**: [`Jenkinsfile`](Jenkinsfile) - uses Microsoft's official `mcr.microsoft.com/playwright`
  agent image (Node.js plus every OS dependency the bundled browsers need, already installed and
  version-matched), so there's no Grid/sidecar container to stand up, same as the C# Playwright sibling.

Both always run headless and with 3 parallel workers by default (`appsettings.ci.json` forces
`Headless: true` whenever the standard `CI=true` env var is present).

## Configuration

`appsettings.json` (defaults) + `appsettings.ci.json` (overrides layered in when `CI=true`) + plain env
vars (highest precedence). See [`src/config/settings.ts`](src/config/settings.ts):

| Setting             | Env var override       | Default                      | Notes                                                                                  |
| ------------------- | ---------------------- | ---------------------------- | -------------------------------------------------------------------------------------- |
| `BaseUrl`           | `BASE_URL`             | `https://www.saucedemo.com/` |                                                                                        |
| `Browser`           | `BROWSER`              | `chromium`                   | `chromium`, `firefox`, or `webkit`                                                     |
| `Headless`          | `HEADLESS`             | `false` (CI: `true`)         |                                                                                        |
| `RemoteUrl`         | `REMOTE_URL`           | `null`                       | Connects to a running Playwright browser server instead of launching locally, when set |
| `ExplicitWaitMs`    | `EXPLICIT_WAIT_MS`     | `20000` (CI: `30000`)        | Context-wide default action/assertion timeout                                          |
| `PageLoadTimeoutMs` | `PAGE_LOAD_TIMEOUT_MS` | `30000` (CI: `45000`)        | Context-wide default navigation timeout                                                |

The env var names are plain and flat (`BASE_URL`, not `TestSettings__BaseUrl`) rather than mirroring the
C# siblings' .NET-style nested-config convention literally - that convention is idiomatic there and
would just look foreign here. The JSON file _shape_ (`{"TestSettings": {...}}`) is kept identical across
all three frameworks on purpose, though, since that's a comparable data format, not a language idiom.

## Project structure

```
src/
  config/       settings.ts, parallelSettings.ts
  browser/      browserManager.ts - one Playwright browser per worker, one context+page per scenario
  users/        userAccount/userCatalog/assignedUser/tagHelpers - the per-worker user assignment
  testdata/     Faker-backed checkout/credential data, fixed product catalog
  pages/        Page objects (basePage + one per saucedemo page)
  support/      Allure attachments, screenshots, environment.properties writer, the retryUntil helper
  steps/        Cucumber step bindings
  hooks.ts      BeforeAll/AfterAll/Before/After lifecycle
  world.ts      Custom Cucumber World (per-scenario session + assigned user)
features/       Gherkin .feature files
tests/          Plain Node test-runner unit tests (user assignment logic - no browser, no Cucumber)
```

## Parallel execution & user assignment

`cucumber.js` sets `parallel: 3`. Each worker is a Node `worker_threads` thread with its own isolated
module scope and a stable `process.env.CUCUMBER_WORKER_ID` ("0", "1", "2", ...) for its whole lifetime,
so `src/users/assignedUser.ts` just maps worker ID to one of the three checkout-capable saucedemo
accounts by index - no leasing, no locking, no release step, unlike the C# siblings' `UserPool`. A
scenario tagged `@user:<username>` (e.g. `@user:locked_out_user`) bypasses that assignment entirely to
target one specific account directly - see [`src/users/tagHelpers.ts`](src/users/tagHelpers.ts).

## Allure reporting

`environment.properties`, `categories.json` and failure-evidence attachments (screenshot, page HTML,
final URL, captured console log) work the same way as both C# siblings - see
[`src/support/`](src/support/). `categories.json` here is evidence-based rather than guessed: every
regex in it was checked against real error output from an actual Playwright run against a live page
(see below) rather than assumed from documentation.

## How this differs from the C# siblings

Porting scenario-for-scenario across ecosystems - not just across browser libraries this time -
surfaced several places where the right design isn't "the same shape with different syntax":

- **Cucumber.js's `--parallel` workers are Node `worker_threads`, not separate OS processes.** This was
  worth being precise about rather than assuming: `worker_threads` are genuinely isolated V8 contexts
  (each with its own copy of every module-level variable), so a `Browser` instance created inside one
  worker is never touched by another thread - safe for exactly the reason the C# Playwright sibling's
  per-_scenario_ browser was needed to avoid a cross-thread hazard under _its_ runtime's shared-memory
  threading. That difference is what makes the standard "one browser per worker, fresh context per
  test" Playwright pattern - the one most tutorials show, and the one the C# port specifically avoided -
  both safe and idiomatic here. See [`src/browser/browserManager.ts`](src/browser/browserManager.ts).
- **User assignment is a fixed per-worker mapping, not a leased-and-returned pool.** `CUCUMBER_WORKER_ID`
  being stable for a worker's whole lifetime means each worker can just own one account outright -
  `src/users/assignedUser.ts` is a few lines instead of a `BlockingCollection`-equivalent.
- **chai's fluent assertions have no built-in "eventually equals" polling constraint** the way NUnit's
  `Is.EqualTo(x).After(ms, interval)` does. `src/support/retryUntil.ts` is the minimal stand-in: poll
  until a predicate passes or a timeout elapses, then let the caller's normal chai assertion run against
  whatever the last poll produced - so a give-up still reads as an ordinary, readable assertion failure.
- **Two ecosystem version-compatibility ceilings, found by actually running `npm install` rather than
  guessing versions** (the same lesson the C# Playwright sibling's `Directory.Packages.props` pin
  learned the hard way, applied here proactively): `typescript-eslint@8.65.0`'s peer dependency caps
  TypeScript at `<6.1.0`, so `typescript` is pinned to `~6.0.3` rather than the newer `7.x` line that
  `npm view` shows as latest. `chai@6.x` ships no bundled types at all (never has) and the newest
  `@types/chai` (`5.2.3`) predates it - installed anyway since the handful of chai APIs this project
  actually uses (`expect`, `.to.equal`, `.to.have.members`, `.to.satisfy`, ...) are long-stable across
  chai's major versions, verified directly (see below).
- **The Playwright _npm_ version is pinned to `1.56.1` exactly**, for the same reason the C# sibling
  pins `Microsoft.Playwright` to `1.56.0`: what matters is which Chromium _build_ a given release
  resolves to (its `browsers.json`), not the package's own version number, and `1.56.1` is the release
  whose bundled Chromium (revision 1194, browserVersion 141.0.7390.37) matches what's actually installed
  in CI/local dev. Confirmed by inspecting the installed package directly, not assumed from the version
  number alone.
- **Every non-obvious Playwright JS/TS API detail here was checked against a live browser, not
  documentation or memory of the .NET binding** - and a few genuinely differ from it:
  - `Microsoft.Playwright`.NET reuses `System.TimeoutException` and has no distinct timeout type; the
    JS/TS binding does the opposite - `errors.TimeoutError` is a real, distinctly-named exported class.
    A strict-mode violation (selector matched more than one element), however, throws a _plain_ `Error`
    in both bindings - confirmed here by deliberately triggering both against a real page and inspecting
    `error.name`/`instanceof`, which is exactly why `waitAndCheckVisible` catches only `TimeoutError` and
    lets anything else (an ambiguous locator) propagate loudly instead of being swallowed into `false`.
  - `page.url()`, `locator.first()`, and `consoleMessage.type()`/`.text()` are _methods_ in JS/TS, not
    properties the way their .NET equivalents are.
  - `context.newContext({ viewport: null })` is the JS/TS equivalent of the .NET binding's
    `ViewportSize.NoViewport` sentinel for opting out of the fixed viewport in headed mode.

## Bugs this framework's own verification run found (and fixed)

The first real `cucumber-js` run of this suite - not just `tsc`/`eslint`/an isolated Playwright smoke
test, but actually loading `cucumber.js`, binding every step, and running the hooks - surfaced four real
bugs that no amount of typechecking or offline smoke-testing could have caught, because each one only
exists at the intersection of this exact config/runtime combination. Fixed here, and recorded because the
fixes look like unrelated nitpicks in isolation:

- **The config file's original shape silently discarded every path in it.** `cucumber.js`'s default
  export was originally `{ default: { paths: [...], import: [...], ... } }`, copying the shape of a CJS
  `module.exports = { default: {...}, ci: {...} }` multi-profile file. That shape is wrong for an ESM
  config file: `import()` yields a module NAMESPACE object whose only own key is always `default`,
  regardless of what the file exports - so the nested `default` key produced a doubly-wrapped object
  that @cucumber/cucumber's `fromFile()` never unwraps. Nothing throws; every path just silently resolves
  to empty, and every step in every scenario reports as `undefined`. Confirmed by running
  `DEBUG=cucumber cucumber-js --dry-run` and watching "Found support files to load via `import` based on
  configuration" come back `[]`. Fixed by making the default export the flat configuration object
  directly - see `cucumber.js`'s own comment.
- **`HookTarget` throws a `SyntaxError` when imported the documented way.** `import { HookTarget } from
'@cucumber/cucumber'` fails at load time in real ESM, even though `HookTarget` is a genuine, correctly
  typed enum on the package. `@cucumber/cucumber@13.2.0`'s ESM entry point
  (`lib/wrapper.mjs`) hand-maintains its own re-export list and simply omits it - confirmed by comparing
  `Object.keys(require('@cucumber/cucumber'))` (has it) against that file's contents (doesn't). Fixed in
  `hooks.ts` by pulling the runtime value through the CJS build via `createRequire`, while keeping it
  nominally typed via a separate `import type`.
- **`--import tsx` does not make `.ts` files work inside `--parallel` worker threads the way it does in
  the coordinator.** Each worker is a real `worker_threads` thread (see `browserManager.ts`) that inherits
  the coordinator's `process.execArgv` for introspection purposes, but Node does not re-run an `--import`-
  registered loader's hooks inside that worker's own module scope - confirmed directly by logging
  `process.execArgv` inside a worker (shows `['--import', 'tsx']`) right before the same worker's `.js`-
  specifier import (relying on tsx's sibling-`.ts` fallback resolution) still threw
  `ERR_MODULE_NOT_FOUND`. Workers fall back to Node's own native TypeScript support instead, which only
  strips erasable syntax: it has no `.js`-to-`.ts` fallback at all, and explicitly rejects constructor
  parameter properties (`ERR_UNSUPPORTED_TYPESCRIPT_SYNTAX`) since those need a real generated
  `this.x = x` assignment, not erasure. Rather than fight that boundary, every internal relative import
  in this project now uses the literal `.ts` extension (`tsconfig.json`'s `allowImportingTsExtensions`),
  and `basePage.ts`'s one constructor-parameter-property was rewritten to a plain field + assignment,
  now enforced by the `@typescript-eslint/parameter-properties` lint rule. See `cucumber.js`'s comment
  for the full chain of reasoning.
- **A duplicated step definition made every scenario that logs in report as "ambiguous."**
  `loginSteps.ts` registered the exact text `"I log in with my assigned user"` twice - once via `Given`,
  once via `When` - because `login.feature`'s smoke scenario phrases it with `When` while
  `checkout.feature`/`cart.feature` use `Given`. Cucumber matches step definitions by text only, never by
  which of `Given`/`When`/`Then` registered them nor which keyword a scenario happens to use, so both
  registrations were equally valid matches for every occurrence - a real duplicate, not two different
  behaviors. Fixed by deleting the redundant registration; the one that's left already covers both
  keywords.

Confirmed fixed by actually running the full suite end-to-end in this environment: `cucumber-js
--dry-run` binds all 13 scenarios / 97 steps with zero undefined or ambiguous steps, and a real (non-
dry-run) run with both `--parallel 1` and `--parallel 3` launches the browser, resolves each worker's
assigned user, and fails every scenario at the exact same point - `page.goto` inside
`createScenarioSession`, with `net::ERR_TUNNEL_CONNECTION_FAILED` - which is this sandbox's own lack of
network access to saucedemo.com, not a code defect (see "Known limitations" below).

## Known limitations

- This framework was locally verified about as far as an environment with no network access to
  saucedemo.com allows: typecheck, lint, format, the browser-independent unit tests, an offline smoke
  test of the real Playwright API against a local page, and - see above - an actual `cucumber-js` run
  (both `--dry-run` and for real, at both `--parallel 1` and `--parallel 3`) confirming every step binds,
  every hook fires, and the browser launches correctly. Every locator here was carried over from the C#
  siblings' locators, which _were_ verified against the real DOM during the Selenium framework's own CI
  debugging - but the actual scenario assertions against the live site (does the cart badge really show
  "1" after adding a product, does the confirmation text really match) can only be confirmed by real CI,
  not by a local run from this environment.
- The `error_user` cart-page-removal scenario (`Cart.feature`) carries the same open question the
  Playwright C# sibling's CI run first surfaced: that scenario has never, in this repo's history, been
  observed to pass against the live site - every prior run failed on an unrelated issue before reaching
  its own assertion. Treat a failure there as expected until that's resolved, not as a bug specific to
  this port.
- `cucumber.js`'s `parallel: 3` is a duplicated literal, not read from `src/config/parallelSettings.ts` -
  see that file's own comment for why (Cucumber reads its config before any loader, including the one
  that would let it import a `.ts` file, is registered). Keep both in sync by hand.

## Adding a scenario / a page object

1. Add the `.feature` scenario first; keep step text close to existing steps so bindings can be reused.
2. New page interactions go on a page object under `src/pages/`, built on `basePage.ts`'s helpers - reach
   for Playwright's own auto-waiting instead of adding manual polling; you almost never need to.
3. If you need a parameterised locator (e.g. "the button inside the card for product X"), prefer a
   Playwright locator (`page.locator('xpath=...')`, or the engine-prefixed selector syntax) over hand-
   rolled DOM traversal. `inventoryPage.ts`'s add/remove-to-cart locators carry over the other two
   frameworks' verified XPath as-is rather than guessing a new one, specifically because there's no way
   to check a new locator against the real DOM from this environment.
4. Run `scripts/run-tests.sh` locally (headed, so you can watch it) before pushing.

## License

[MIT](../LICENSE)
