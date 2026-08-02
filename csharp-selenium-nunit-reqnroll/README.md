# csharp-selenium-nunit-reqnroll

[![CI](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/csharp-selenium-nunit-reqnroll.yml/badge.svg)](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/csharp-selenium-nunit-reqnroll.yml)

Selenium UI tests for [saucedemo.com](https://www.saucedemo.com/), written in C# with NUnit as the
runner, [Reqnroll](https://reqnroll.net/) (the actively-maintained SpecFlow successor) for Gherkin
BDD, [Allure](https://allurereport.org/) for reporting, and [Bogus](https://github.com/bchavez/Bogus)
for fake test data. Scenarios run **in parallel**, and each concurrently-running scenario logs in
with its **own, distinct saucedemo user** - no two parallel sessions ever share an account.

## Stack

| Concern | Choice |
|---|---|
| Language / SDK | C# / .NET 10 |
| Browser automation | Selenium WebDriver 4 (Selenium Manager resolves ChromeDriver automatically - nothing to install) |
| Test runner | NUnit 4, parallel execution enabled |
| BDD | Reqnroll (Gherkin `.feature` files + step definitions) |
| Test data | Bogus (checkout form data only - login users are fixed, real accounts) |
| Reporting | Allure (via `Allure.Reqnroll`) |
| CI | GitHub Actions (primary) + Jenkinsfile (secondary) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Chrome install (Selenium Manager downloads a matching ChromeDriver on first run and caches it
  under `~/.cache/selenium`)
- Optional, for viewing reports locally: the [Allure commandline](https://allurereport.org/docs/gettingstarted-installation/)
  (`npm i -g allure-commandline`, `brew install allure`, or `scoop install allure`)

## Quick start

```bash
cd csharp-selenium-nunit-reqnroll
dotnet restore UiTests.sln
dotnet build UiTests.sln
scripts/run-tests.sh            # or scripts\run-tests.ps1 on Windows
scripts/generate-report.sh      # opens the Allure report in a browser
```

## Project structure

```
csharp-selenium-nunit-reqnroll/
├── UiTests.sln
├── Directory.Build.props / Directory.Packages.props   # net10.0, central package versions
├── Jenkinsfile                                         # secondary CI
├── scripts/                                            # run-tests / generate-report (sh + ps1)
└── SauceDemo.UiTests/
    ├── AssemblyInfo.cs        # NUnit parallelism switches - see below
    ├── reqnroll.json / allureConfig.json / categories.json
    ├── appsettings.json / appsettings.ci.json
    ├── Configuration/         # TestSettings, ParallelSettings, ConfigurationLoader
    ├── Drivers/                # WebDriverFactory, BrowserSession (Selenium Manager, headless, remote)
    ├── Users/                  # UserAccount, UserCatalog, UserPool, UserLease - the parallel-user mechanism
    ├── Pages/                  # Page objects (BasePage + one per saucedemo page)
    ├── Support/                # Screenshot/Allure-attachment/environment.properties helpers
    ├── Hooks/                  # TestRunHooks (once per run) + ScenarioHooks (once per scenario)
    ├── TestData/                # Bogus factories + fixed product catalog + ScenarioState
    ├── Features/                # Login.feature, Checkout.feature, Cart.feature
    ├── StepDefinitions/         # One class per feature's domain
    └── Tests/                   # Plain NUnit unit tests for UserPool (no browser)
```

## How parallel execution works

```
[assembly: Parallelizable(ParallelScope.Children)]   // AssemblyInfo.cs
[assembly: LevelOfParallelism(3)]                    // = ParallelSettings.MaxParallelWorkers
```

Reqnroll generates one NUnit `[Test]` per scenario (and per Scenario Outline Examples row), so
`ParallelScope.Children` runs scenarios concurrently - including several scenarios from the same
feature file. With `LevelOfParallelism(3)`, up to 3 scenarios run at once, each in its own Chrome
session:

```
worker 1 ──▶ acquire user ──▶ standard_user            ──▶ own Chrome session
worker 2 ──▶ acquire user ──▶ performance_glitch_user   ──▶ own Chrome session
worker 3 ──▶ acquire user ──▶ visual_user               ──▶ own Chrome session
```

`Users/UserPool` is a thread-safe lease/return pool (`BlockingCollection<UserAccount>`), built once
in `[BeforeTestRun]` and shared by every worker. `Hooks/ScenarioHooks` acquires a lease in
`[BeforeScenario]` and releases it in `[AfterScenario]` (after the browser has already quit), so a
user is never in use by two scenarios at the same time. **The pool size must be ≥
`LevelOfParallelism`** - `UserPool`'s constructor asserts this at startup, and `Acquire` throws a
diagnosable `TimeoutException` (naming the pool's current availability) rather than hanging forever
if a scenario can't get a user within 2 minutes.

The driver itself is never `ThreadLocal` or static: Reqnroll gives every scenario its own DI
container (BoDi), and `ScenarioHooks` registers that scenario's `IWebDriver` and `UserAccount` into
it, so step definitions just constructor-inject `IWebDriver` for a session that is intrinsically
scoped to their own scenario - safe even if a step is ever made `async` (which `ThreadLocal` is not).

### The saucedemo user roster

Only 3 of saucedemo's 6 accounts can complete a full purchase, so only those 3 are in the parallel
pool (`Users/UserCatalog.PoolUsers`). The other 3 are deliberately broken and are instead targeted
directly by scenarios tagged `@user:<username>`, bypassing the pool entirely:

| User | Login | Full checkout | Notes |
|---|---|---|---|
| `standard_user` | ✅ | ✅ | baseline - in the pool |
| `performance_glitch_user` | ✅ | ✅ | ~5s artificial delays - in the pool |
| `visual_user` | ✅ | ✅ | cosmetic-only defects - in the pool |
| `problem_user` | ✅ | ❌ | checkout last-name field is broken - targeted via `@user:problem_user` |
| `error_user` | ✅ | ❌ | fails on cart removal / checkout completion - targeted via `@user:error_user` |
| `locked_out_user` | ❌ | ❌ | login rejected by design - targeted via `@user:locked_out_user` |

Raising `LevelOfParallelism` above 3 without adding more accounts to `PoolUsers` will make the extra
workers block on `Acquire` until an earlier scenario finishes.

## Configuration reference

Settings live under the `TestSettings` section of `appsettings.json`; `appsettings.ci.json` layers on
top whenever the standard `CI=true` env var is present (GitHub Actions sets this automatically).
Every key can also be overridden with a `TestSettings__<Key>` environment variable.

| Key | Default | Purpose |
|---|---|---|
| `BaseUrl` | `https://www.saucedemo.com/` | Site under test |
| `Browser` | `Chrome` | `Chrome`, `Firefox`, or `Edge` |
| `Headless` | `false` (`true` under CI) | Headless browser mode |
| `RemoteUrl` | *(none)* | Selenium Grid / remote endpoint, e.g. `http://localhost:4444/wd/hub`. Set to run against a container instead of a local browser - no code changes needed. |
| `ExplicitWaitSeconds` | `20` | Explicit wait used by every page-object interaction |
| `PageLoadTimeoutSeconds` | `30` | Page-load timeout |

## Running a subset of scenarios

```bash
dotnet test UiTests.sln --filter "Category=smoke"
dotnet test UiTests.sln --filter "Category=negative"
dotnet test UiTests.sln -- NUnit.NumberOfTestWorkers=1   # override LevelOfParallelism for local debugging
```

Tag a feature `@serial` to opt it out of parallel execution entirely (see `reqnroll.json`'s
`addNonParallelizableMarkerForTags`).

## Allure report

`Allure.Reqnroll` writes results to `SauceDemo.UiTests/bin/<Configuration>/net10.0/allure-results`
(relative to the test output directory, not the repo root). `scripts/generate-report.sh` /
`.ps1` copy `categories.json` in and open the report with the Allure CLI. In CI, the GitHub Actions
workflow uploads `allure-results` as a build artifact and publishes a generated report to
`gh-pages` with history/trend graphs.

Every scenario carries `user` and `worker` parameters (set in `ScenarioHooks`) so the report shows
which saucedemo account and which NUnit worker actually ran it - the visible proof that parallel
scenarios really did use distinct users. On failure, a screenshot, the page source, the final URL,
and (where supported) the browser console log are attached automatically.

## Adding a scenario / a page object

1. Add or extend a `.feature` file under `Features/`.
2. Add matching step methods to the relevant class under `StepDefinitions/` (constructor-inject
   `IWebDriver`, `UserAccount`, and/or `ScenarioState` as needed - all three are registered per
   scenario by `ScenarioHooks`).
3. Add a page object under `Pages/` if the scenario touches a new page: extend `BasePage`, take only
   `IWebDriver` in the constructor, and use its `Click`/`Type`/`TextOf`/`IsVisible` helpers - never
   `Thread.Sleep`. Prefer `data-test` attribute locators (saucedemo ships them on every interactive
   element) over CSS classes.

## Parallel-safety notes (read before touching Hooks/ or TestData/)

- **Never use `[BeforeFeature]`/`[AfterFeature]` or `FeatureContext`/`ScenarioContext.Current`.**
  Reqnroll does not guarantee a single `FeatureContext` instance under scenario-level parallelism, and
  the `.Current` static accessors throw under parallel execution. Use constructor-injected
  `ScenarioContext` instead (see `Hooks/TagHelpers.cs`).
- **Never cache a `Faker<T>` in a static field.** `Faker<T>` instances are not thread-safe;
  `TestData/CheckoutDataFactory.Create()` builds a fresh one per call on purpose.
- **Never set `Bogus.Randomizer.Seed`.** It's a global static and not thread-safe. Use the
  instance-level `Faker<T>.UseSeed(...)` if you need reproducibility.
- **No mutable static state** outside `Users.UserPool` (which is intentionally a thread-safe
  singleton). Everything else should be a scenario-scoped instance (constructor-injected) or a plain
  local.

## Known limitations / upgrade paths

- Only 3 saucedemo accounts can complete checkout, which caps meaningful in-pool parallelism at 3.
  This is enforced at startup by `UserPool`, not discovered mid-run.
- `saucedemo.com` is a public demo site with no uptime SLA; the CI workflow's nightly schedule exists
  to catch drift before it blocks a PR.
- The user pool is a `static` singleton for simplicity. For a larger suite, swap it for Reqnroll's
  `Reqnroll.Microsoft.Extensions.DependencyInjection` plugin and register it as a run-scoped service.
- `Firefox`/`Edge` are supported by `WebDriverFactory` but only Chrome is installed in CI.
