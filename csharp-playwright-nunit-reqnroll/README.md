# csharp-playwright-nunit-reqnroll

C# UI test automation for [saucedemo.com](https://www.saucedemo.com/), built with Playwright,
NUnit (parallel), Reqnroll (Gherkin/BDD) and Allure reporting. This is the Playwright sibling of
[`csharp-selenium-nunit-reqnroll`](../csharp-selenium-nunit-reqnroll/) - same stack, same site, same
scenario coverage, different browser-automation library. See
["How this differs from the Selenium sibling"](#how-this-differs-from-the-selenium-sibling) below for
what actually changed and why, rather than just swapping one API for another.

## Stack

- **.NET 10** / C#, NUnit 4 (parallel execution)
- **Playwright** for browser automation (Chromium by default; Firefox and WebKit also supported)
- **Reqnroll** for Gherkin feature files and step bindings
- **Allure** (`Allure.Reqnroll`) for HTML test reports
- **Bogus** for fake test data (never for login credentials - see [`Users/UserCatalog.cs`](SauceDemo.UiTests/Users/UserCatalog.cs))

## Prerequisites

- .NET SDK 10.0.100+ (see [`global.json`](global.json))
- Playwright's browser binaries. After the first build, install them once:

  ```bash
  # bash/macOS/Linux
  pwsh SauceDemo.UiTests/bin/Debug/net10.0/playwright.ps1 install chromium
  # or, without PowerShell:
  SauceDemo.UiTests/bin/Debug/net10.0/playwright.sh install chromium
  ```

  (Swap `chromium` for `firefox` or `webkit`, or drop the argument to install all three, if you plan
  to run with `TestSettings:Browser` set to something other than the default.)

## Running locally

```bash
scripts/run-tests.sh              # 3 parallel workers, all scenarios
scripts/run-tests.sh 3 smoke       # 3 parallel workers, @smoke only
```

(`scripts/run-tests.ps1` is the PowerShell equivalent.) By default this runs headed (a visible
browser window), since `appsettings.json` sets `Headless: false`. After a run:

```bash
scripts/generate-report.sh   # or generate-report.ps1
```

opens the Allure report for the results just produced.

## Running in CI

- **GitHub Actions**: [`.github/workflows/csharp-playwright-nunit-reqnroll.yml`](../.github/workflows/csharp-playwright-nunit-reqnroll.yml),
  triggered on push/PR to this folder, `workflow_dispatch` (custom worker count / tag filter), and a
  nightly schedule. Publishes an Allure report to GitHub Pages under `playwright/<run-number>/` and
  `playwright/latest/` (namespaced so it never collides with the Selenium sibling's own reports on the
  same `gh-pages` branch).
- **Jenkins**: [`Jenkinsfile`](Jenkinsfile) - uses Microsoft's official `mcr.microsoft.com/playwright/dotnet`
  agent image (.NET SDK plus every OS dependency the bundled browsers need, already installed and
  version-matched), so - unlike the Selenium sibling's Jenkinsfile - there's no Grid/sidecar container
  to stand up first.

Both always run headless and with 3 parallel workers by default (`appsettings.ci.json` forces
`Headless: true` whenever the standard `CI=true` env var is present).

## Configuration

`appsettings.json` (defaults) + `appsettings.ci.json` (overrides layered in when `CI=true`) +
`TestSettings__*` environment variables (highest precedence). See [`TestSettings.cs`](SauceDemo.UiTests/Configuration/TestSettings.cs):

| Setting | Default | Notes |
|---|---|---|
| `BaseUrl` | `https://www.saucedemo.com/` | |
| `Browser` | `Chromium` | `Chromium`, `Firefox`, or `WebKit` |
| `Headless` | `false` (CI: `true`) | |
| `RemoteUrl` | `null` | Connects to a running Playwright browser server (`playwright run-server`) instead of launching locally, when set |
| `ExplicitWaitSeconds` | `20` (CI: `30`) | Context-wide default action/assertion timeout |
| `PageLoadTimeoutSeconds` | `30` (CI: `45`) | Context-wide default navigation timeout |
| `UserAcquireTimeoutSeconds` | `120` | How long a scenario waits for a free pooled saucedemo account |

## Project structure

```
SauceDemo.UiTests/
  Configuration/   TestSettings, ConfigurationLoader, ParallelSettings
  Drivers/         PlaywrightFactory (builds one scenario's Playwright stack), BrowserSession, BrowserType
  Users/           UserAccount/UserCatalog/UserPool/UserLease - the parallel-safe login-user pool
  TestData/        Bogus-backed checkout/credential data, fixed product catalog
  Pages/           Page objects (BasePage + one per saucedemo page)
  Hooks/           Reqnroll [BeforeScenario]/[AfterScenario]/[BeforeTestRun] lifecycle, tag helpers
  Support/         Allure attachments, screenshot capture, environment.properties writer
  Features/        Gherkin .feature files
  StepDefinitions/ Reqnroll step bindings
  Tests/           Plain NUnit unit tests (UserPool concurrency - no browser, no Reqnroll)
```

## Parallel execution & the user pool

Identical model to the Selenium sibling: `[assembly: Parallelizable(ParallelScope.Children)]` +
`[assembly: LevelOfParallelism(3)]` run one NUnit `[Test]` per scenario, 3 at a time, on 3 threads
*inside one process*. `Users/UserPool` hands each concurrently-running scenario a distinct saucedemo
login account (of the 3 checkout-capable ones) so parallel scenarios never race on the same account;
`Hooks/ScenarioHooks` acquires one on `[BeforeScenario]` and releases it on `[AfterScenario]`. A
scenario tagged `@user:<username>` (e.g. `@user:locked_out_user`) bypasses the pool entirely to target
one specific account directly - see [`Hooks/TagHelpers.cs`](SauceDemo.UiTests/Hooks/TagHelpers.cs).

## Allure reporting

`environment.properties`, `categories.json` and failure-evidence attachments (screenshot, page HTML,
final URL, captured console log) work the same way as the Selenium sibling - see
[`Support/`](SauceDemo.UiTests/Support/). The one structural difference: Playwright's console log is
push-based (`IPage.Console` event), not pull-based like Selenium's `Manage().Logs.GetLog(...)`, so
`BrowserSession` subscribes to it the moment the page is created and accumulates messages for the
scenario's lifetime, rather than asking for "everything logged so far" after the fact.

## How this differs from the Selenium sibling

Porting scenario-for-scenario rather than mechanically translating API calls surfaced several places
where the right design genuinely isn't "the same shape with different method names":

- **Everything is still scoped per scenario - deliberately, not just for parity.** The pattern most
  Playwright tutorials show is one shared `IBrowser` per worker with a fresh `IBrowserContext` per
  test. That assumes process-per-worker parallelism. This project's parallelism
  (`[assembly: LevelOfParallelism(3)]` + `ParallelScope.Children`) runs scenarios on multiple *threads
  inside one process* instead, and Playwright's own guidance is that its objects (page/context/browser/
  the driver connection itself) aren't safe to touch from more than one thread at a time. So
  `Drivers/PlaywrightFactory` builds a whole new `IPlaywright` + `IBrowser` + `IBrowserContext` + `IPage`
  stack per scenario instead - heavier than the tutorial pattern, but it sidesteps that hazard entirely,
  and it's the same trade-off the Selenium sibling already made by launching a whole new browser process
  per scenario.
- **`BrowserType` is `Chromium` / `Firefox` / `WebKit`, not `Chrome` / `Firefox` / `Edge`.** Playwright
  downloads and drives specific engine builds directly rather than automating a locally-installed
  vendor browser; "Chrome" and "Edge" aren't separate download targets (both are Chromium under the
  hood, reachable only via the `Channel` launch option against an already-installed copy - not
  guaranteed to exist in CI or a fresh dev box the way the bundled engines are). WebKit is a genuine
  gain: Safari's engine, running on Linux CI, with no macOS machine required.
- **Most of `BasePage`'s Selenium-side plumbing is just gone.** Playwright locators are lazy (they
  re-query the DOM on every use - there's no stale-element concept at all) and every action auto-waits
  for the target to be attached, visible, stable and actually hit-testable before doing anything. The
  "WaitForClickable, then Click, retrying on `StaleElementReferenceException`" dance that made up most
  of the Selenium sibling's `BasePage` isn't needed here - what's left is two small helpers for "wait
  for this or fail loudly" and "wait for this and tell me whether it showed up, without throwing."
- **`InventoryPage.ClickAndConfirmToggleAsync` still exists, on purpose.** The Selenium sibling's
  original version of this existed because a WebDriver click that reported success was sometimes
  followed by the cart state never actually changing. Playwright's stronger actionability checks make
  that specific race meaningfully less likely - but the Selenium sibling's own README documents that its
  *remaining* CI flakiness was traced (screenshot/DOM/JS-bundle evidence, not guesswork) most likely to
  saucedemo.com itself degrading under repeated automated traffic, not a client-side timing bug. No
  client library, however good its waiting, can wait out a server that never changes state, so the
  bounded retry is kept here as cheap defense in depth - not a sign this framework has the bug the
  Selenium one did.
- **No temp `--user-data-dir` to manage.** The Selenium sibling creates and cleans up its own per-session
  temp profile directory specifically to stop concurrent sessions from colliding on one Chrome profile.
  A plain (non-persistent) `IBrowserContext` is fully isolated - cookies, local storage, cache - and
  in-memory by construction, so that whole category of plumbing (and cleanup-on-dispose code) simply
  doesn't exist in `Drivers/BrowserSession.cs`.
- **The headless-viewport trap structurally cannot happen here.** The Selenium sibling's README
  documents, at length, a real bug: `Window.Maximize()` in headless Chrome/Edge shrinks the window to
  an 800x600 headless virtual screen instead of enlarging it, silently undoing `--window-size` and
  running the whole suite in saucedemo's narrow/mobile layout. Playwright never resizes a native OS
  window at all - it sets the viewport directly via `BrowserNewContextOptions.ViewportSize` - so there
  is no OS-level maximize step for headless mode to trip over in the first place.
- **`FillAsync`, not simulated keystrokes.** saucedemo's login/checkout forms have no per-keystroke
  behaviour (autocomplete, incremental validation) that needs real typing, so the faster, less flaky
  direct fill is the right default rather than `PressSequentiallyAsync`.
- **One naming trap worth knowing about if you extend this code:** `Microsoft.Playwright`'s only public
  exception type is `PlaywrightException` - there is no `Microsoft.Playwright.TimeoutException`. Every
  Playwright failure, including a timed-out wait, surfaces as a `PlaywrightException` (its message
  contains `"Timeout ...ms exceeded"` when that's the cause - see `categories.json`'s message-based
  "Timeouts" category, since a trace-based match on a type name isn't possible here the way the Selenium
  sibling's `categories.json` matches `WebDriverTimeoutException`).
- **Pinning the Playwright version.** `Microsoft.Playwright`'s NuGet releases don't always mirror npm's
  patch versions 1:1 (e.g. there is no `1.56.1` on NuGet even though npm has one) - what actually matters
  is which Chromium *build* a given package version resolves to (its embedded `browsers.json`), not the
  package's own version number in isolation. `Directory.Packages.props` pins to the release whose
  bundled Chromium matches what CI/local dev actually installs, for the exact same reason the Selenium
  sibling has to keep its ChromeDriver-resolving Selenium Manager version aligned with the Chrome build
  under test - a mismatch there is a version-skew bug, not a code bug.

## Known limitations

- This framework was built and locally verified (build, `dotnet format`, the browser-independent
  `UserPoolTests`, and an offline smoke test of `PlaywrightFactory`/`BrowserSession` launching a real
  headless browser and navigating/clicking/screenshotting a local page) in an environment with no
  network access to saucedemo.com itself. Every locator here was carried over from the Selenium
  sibling's locators, which *were* verified against the real DOM during that framework's CI debugging -
  but the actual Gherkin-to-step-definition scenario runs against the live site have only been verified
  by (a) a full text-level cross-check that every concrete step in every `.feature` file matches exactly
  one step-definition regex, and (b) real CI, not by a local run against saucedemo.com. If a scenario
  fails in CI, treat the Selenium sibling's README - especially the account-behaviour notes for
  `error_user`/`problem_user`/`performance_glitch_user` and the "cart is client-side, not persisted"
  point - as the first place to look before assuming this framework has a new bug.
- `reqnroll.json` sets `missingOrPendingStepsOutcome: Error` from the start (not the Reqnroll default of
  `Inconclusive`, which `dotnet test` does not count as a failure). This was the single most consequential
  fix to come out of the Selenium sibling's debugging history - it was set here from the outset rather
  than being rediscovered the hard way.

## Adding a scenario / a page object

1. Add the `.feature` scenario first; keep step text close to existing steps so bindings can be reused.
2. If a step needs a new binding, make sure the Gherkin keyword you write (`Given`/`When`/`Then`) and
   the attribute you bind it with agree - `And`/`But` inherit the preceding step's effective type.
3. New page interactions go on a page object under `Pages/`, built on `BasePage`'s helpers - reach for
   Playwright's own auto-waiting instead of adding manual polling; you almost never need to.
4. If you need a parameterised locator (e.g. "the button inside the card for product X"), prefer a
   Playwright locator (`Page.Locator("xpath=...")`, or the engine-prefixed selector syntax) over hand-
   rolled DOM traversal. `InventoryPage`'s add/remove-to-cart locators carry over the Selenium sibling's
   verified XPath as-is rather than guessing a new one, specifically because there's no way to check a
   new locator against the real DOM from this environment.
5. Run `scripts/run-tests.sh` locally (headed, so you can watch it) before pushing.

## License

[MIT](../LICENSE)
