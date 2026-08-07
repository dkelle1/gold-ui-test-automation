# java-selenium-junit5-cucumber

[![CI](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/java-selenium-junit5-cucumber.yml/badge.svg)](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/java-selenium-junit5-cucumber.yml)

Selenium UI tests for [saucedemo.com](https://www.saucedemo.com/), written in Java with
[Cucumber-JVM](https://cucumber.io/docs/installation/java/) for Gherkin BDD running on the JUnit
Platform, [Allure](https://allurereport.org/) for reporting, and
[Datafaker](https://www.datafaker.net/) for fake test data. Scenarios run **in parallel**, and each
concurrently-running scenario logs in with its **own, distinct saucedemo user** - no two parallel
sessions ever share an account.

## Stack

| Concern | Choice |
|---|---|
| Language / build | Java 21 / Maven |
| Browser automation | Selenium WebDriver 4 (Selenium Manager resolves ChromeDriver automatically - nothing to install) |
| Test runner | JUnit Platform, via `cucumber-junit-platform-engine`, parallel execution enabled |
| BDD | Cucumber-JVM (Gherkin `.feature` files + step definitions), PicoContainer for per-scenario DI |
| Test data | Datafaker (checkout form data only - login users are fixed, real accounts) |
| Reporting | Allure (via `allure-cucumber7-jvm`) |
| CI | GitHub Actions (primary) + Jenkinsfile (secondary) |

## Prerequisites

- JDK 21 and Maven 3.9+
- A Chrome install (Selenium Manager downloads a matching ChromeDriver on first run and caches it
  under `~/.cache/selenium`)
- Optional, for viewing reports locally: the [Allure commandline](https://allurereport.org/docs/gettingstarted-installation/)
  (`npm i -g allure-commandline`, `brew install allure`, or `scoop install allure`)

## Quick start

```bash
cd java-selenium-junit5-cucumber
mvn test-compile
scripts/run-tests.sh            # or scripts\run-tests.ps1 on Windows
scripts/generate-report.sh      # opens the Allure report in a browser
```

## Project structure

```
java-selenium-junit5-cucumber/
├── pom.xml                     # dependencies + Surefire wiring
├── Jenkinsfile                 # secondary CI
├── appsettings.json / appsettings.ci.json / categories.json
├── features/                   # login.feature, checkout.feature, cart.feature
├── scripts/                    # run-tests / generate-report (sh + ps1)
└── src/test/
    ├── resources/
    │   ├── junit-platform.properties   # parallelism, glue package, Allure plugin
    │   └── allure.properties
    └── java/com/saucedemo/uitests/
        ├── RunCucumberTest.java        # the @Suite entry point Surefire runs
        ├── config/                     # TestSettings, ConfigurationLoader, ParallelSettings
        ├── drivers/                    # WebDriverFactory, BrowserSession (headless, remote, temp profile)
        ├── users/                      # UserAccount, UserCatalog, UserPool, UserLease - the parallel-user mechanism
        ├── pages/                      # BasePage + one page object per saucedemo page
        ├── support/                    # Polling, screenshot / Allure-attachment / environment.properties helpers
        ├── hooks/                      # ScenarioContext (per-scenario state) + ScenarioHooks (lifecycle)
        ├── testdata/                   # Datafaker factories + fixed product catalog
        ├── steps/                      # One class per feature's domain
        └── unit/                       # Plain JUnit tests for UserPool (no browser)
```

## How parallel execution works

```properties
# src/test/resources/junit-platform.properties
cucumber.execution.parallel.enabled=true
cucumber.execution.parallel.config.strategy=fixed
cucumber.execution.parallel.config.fixed.parallelism=3
```

Cucumber reports every scenario (and every Scenario Outline Examples row) as its own platform test, so
`parallelism=3` means up to 3 scenarios in flight at once, each in its own Chrome session:

```
scenario A ──▶ lease user ──▶ standard_user            ──▶ own Chrome session
scenario B ──▶ lease user ──▶ performance_glitch_user  ──▶ own Chrome session
scenario C ──▶ lease user ──▶ visual_user              ──▶ own Chrome session
```

`users/UserPool` is a thread-safe lease/release pool (`LinkedBlockingQueue<UserAccount>`), created once
per JVM by `SharedUserPool` and shared by every scenario. `hooks/ScenarioHooks` leases an account before
opening the browser and releases it only after that browser has fully quit, so an account is never in use
by two scenarios at once.

### Why a lease pool here, when two siblings just use the worker index

This is the most interesting difference between the frameworks in this gallery, and it is forced by the
runner, not a matter of taste:

| Framework | Runner's parallel unit | Stable per-worker id? | Account assignment |
|---|---|---|---|
| `java-selenium-junit5-cucumber` | threads on a shared pool (JUnit Platform) | **no** | lease/release pool |
| `csharp-*-nunit-reqnroll` | threads in one process (NUnit) | no | lease/release pool |
| `ts-playwright-cucumber` | Node `worker_threads` | yes (`CUCUMBER_WORKER_ID`) | fixed index mapping |
| `python-selenium-pytest-bdd` | separate OS processes (pytest-xdist) | yes (`PYTEST_XDIST_WORKER`) | fixed index mapping |

Cucumber.js and pytest-xdist give each worker a stable identity for its whole lifetime, so "worker N
always gets account N" is both possible and simpler there. The JUnit Platform instead dispatches
scenarios onto a shared thread pool with no stable per-worker index to key off - a thread is just
whichever pool thread happened to pick the scenario up. Leasing an account for the duration of a scenario
and returning it afterwards is therefore the model that actually fits.

### The pool-size guard cannot drift here

Every framework in this gallery has to keep "how many workers" and "how many accounts" in agreement. The
siblings do it by convention: the C# ones need a compile-time constant for
`[assembly: LevelOfParallelism(...)]`, and the Python one's own comment concedes that keeping its constant
in sync "is entirely a matter of convention".

`config/ParallelSettings` instead reads the parallelism back from the same two places the Cucumber engine
reads it from, in the same precedence order (system property, then `junit-platform.properties`), so
`UserPool`'s constructor validates against the number the engine will *actually* use. Raising the
parallelism without adding accounts fails immediately, with a message naming both numbers, rather than
silently letting two scenarios share a login.

## The saucedemo user roster

Only 3 of saucedemo's 6 accounts can complete a full purchase, so only those 3 are in the pool
(`users/UserCatalog.POOL_USERS`, derived automatically from the `canCompleteCheckout` capability flag
rather than listed by hand). The others are deliberately broken and are targeted directly by scenarios
tagged `@user:<username>`, bypassing the pool entirely:

| User | Login | Full checkout | Notes |
|---|---|---|---|
| `standard_user` | ✅ | ✅ | baseline - in the pool |
| `performance_glitch_user` | ✅ | ✅ | ~5s artificial delays - in the pool |
| `visual_user` | ✅ | ✅ | cosmetic-only defects - in the pool |
| `problem_user` | ✅ | ❌ | checkout last-name field is broken - targeted via `@user:problem_user` |
| `error_user` | ✅ | ❌ | fails on cart removal / checkout completion - not currently targeted by a scenario (the cart-removal scenario that used to exercise it never passed reliably against the live site, so it was removed as flaky across all frameworks; kept in the roster for completeness) |
| `locked_out_user` | ❌ | ❌ | login rejected by design - targeted via `@user:locked_out_user` |

## Configuration reference

Settings live under the `TestSettings` section of `appsettings.json`; `appsettings.ci.json` layers on top
whenever the standard `CI=true` env var is present (GitHub Actions sets this automatically). Every key can
also be overridden with the env var named below, which always wins.

| Key | Env var | Default | Purpose |
|---|---|---|---|
| `BaseUrl` | `BASE_URL` | `https://www.saucedemo.com/` | Site under test |
| `Browser` | `BROWSER` | `chrome` | `chrome`, `firefox`, or `edge` |
| `Headless` | `HEADLESS` | `false` (`true` under CI) | Headless browser mode |
| `RemoteUrl` | `REMOTE_URL` | *(none)* | Selenium Grid / remote endpoint, e.g. `http://localhost:4444/wd/hub`. Set to run against a container instead of a local browser - no code changes needed (this is what the Jenkinsfile uses). |
| `ExplicitWaitSeconds` | `EXPLICIT_WAIT_SECONDS` | `20` | Explicit wait used by every page-object interaction |
| `PageLoadTimeoutSeconds` | `PAGE_LOAD_TIMEOUT_SECONDS` | `30` | Page-load timeout |

## Running a subset of scenarios

```bash
mvn test -Dcucumber.filter.tags="@smoke"
mvn test -Dcucumber.filter.tags="@negative and not @known-issue"

# Serial, for local debugging - the pool guard allows fewer workers than accounts, just never more.
mvn test -Dcucumber.execution.parallel.config.fixed.parallelism=1

# Prove every step in every feature is bound, without launching a browser.
mvn test -Dcucumber.execution.dry-run=true
```

## Allure report

Results are written to `allure-results/` at the framework root (see `src/test/resources/allure.properties`).
`scripts/generate-report.sh` copies `categories.json` in and opens the report. In CI, the workflow uploads
the raw results, publishes a full report to `gh-pages` under `java-selenium/`, and also attaches a
self-contained single-file HTML report as a downloadable artifact.

Every scenario carries `user` and `worker` parameters set in `ScenarioHooks`, so the report shows which
saucedemo account and which thread actually ran it - the visible proof that parallel scenarios really did
use distinct accounts. On failure, a screenshot, the page source, the final URL, and (on Chrome/Edge) the
browser console log are attached automatically.

## Adding a scenario or a page object

1. Add or extend a `.feature` file under `features/`.
2. Add step methods to the relevant class under `steps/`. Every step class constructor-injects
   `ScenarioContext`, which carries that scenario's driver and assigned user; PicoContainer supplies the
   same instance to every class in the scenario.
3. Add a page object under `pages/` if the scenario touches a new page: extend `BasePage`, take only
   `WebDriver` in the constructor, and use its waiting `click`/`typeText`/`textOf` helpers - never a bare
   `Thread.sleep`. Prefer `data-test` attribute locators (saucedemo ships them on every interactive
   element) over CSS classes.

## Parallel-safety notes (read before touching hooks/ or testdata/)

- **No mutable static state** except the intentionally thread-safe `SharedUserPool`. Per-scenario state
  belongs on `ScenarioContext`, which PicoContainer scopes for you.
- **`@Before` and `@After` order values run in opposite directions.** Per Cucumber's own javadoc,
  `@Before` runs *lower* values first and `@After` runs *higher* values first, so teardown mirrors setup.
  This is the reverse of Reqnroll in the C# siblings, and getting it backwards here would quit the browser
  before the failure screenshot was taken.
- **Never share a `Faker` instance.** Datafaker draws from a `Random` that is not safe for concurrent use;
  `testdata/CheckoutDataFactory` builds a fresh one per call for exactly that reason, and seeds through
  that instance rather than any global seed.
- **Do not let Surefire fork as well.** The parallelism that matters is Cucumber's, inside one JVM;
  turning on Surefire's own forking too would multiply the two and blow past the user pool.

## Known limitations

- Only 3 saucedemo accounts can complete checkout, capping meaningful parallelism at 3. This is enforced
  at startup by `UserPool`'s constructor, not discovered mid-run.
- `saucedemo.com` is a public demo site with no uptime SLA; the workflow's nightly schedule exists to
  catch DOM/behaviour drift before it blocks a PR.
- **Confirmed in this framework's first real CI run: every scenario that adds a product to the cart can
  fail with `'<product>' was never actually added to the cart` after 3 retries.** 7 of the 12 scenarios
  failed that way (all 5 login scenarios, which touch no cart, passed, as did all 9 unit tests). This is
  the same failure - same point, same retry count, same message shape - that the C# Selenium sibling
  documents for its own `ClickAndConfirmToggle` and that the Python Selenium sibling recorded on *its*
  first CI run. In the run where all four earlier frameworks went out together, both Selenium-based ones
  failed these scenarios and both Playwright-based ones passed cleanly.

  That split is the evidence: this is a Selenium/live-site click-registration quirk that this port
  reproduces faithfully, not something introduced here. Every documented lesson from the siblings is
  already applied - notably no `maximize()` in headless (which would silently drop the suite into
  saucedemo's narrow layout), and locators checked against the shape recorded from real failing CI page
  source. Consistent with both siblings, it is documented rather than worked around speculatively: a
  different retry count is not known to fix it, and switching to a JavaScript click would make this
  framework's interaction model quietly different from the other Selenium one, which would undercut the
  point of comparing the stacks side by side.
- **`I go to the cart` may be the next thing to fail once add-to-cart succeeds.** The C# Selenium sibling
  records that its equivalent `OpenCart()` has never yet been observed to work in CI - the click on
  `[data-test='shopping-cart-link']` reports success and the browser stays on `/inventory.html` - with a
  suspected header-versus-grid split. It is currently masked here too, because add-to-cart fails first.
- **A cart-contents assertion that reports the full 6-product catalog means the browser is still on the
  inventory page, not that the cart is polluted.** `CartPage.listItemNames` and
  `InventoryPage.listProductNames` both read `[data-test='inventory-item-name']`, which exists on both
  pages. Check the `final-url` attachment first - it says which page the assertion actually ran against.
- **No automatic retry of failed scenarios**, by design and matching the siblings: saucedemo has no SLA,
  so a real outage would be silently masked by retries.
- **What has and hasn't been verified locally.** Verified by actually running it: the build; the unit
  tests, including a multi-threaded one asserting no two threads ever hold the same account; a full
  Cucumber dry-run proving every step in every feature binds to exactly one step definition; tag
  filtering; and a real (non-dry-run) parallel execution, which confirmed the hook chain, the
  `@BeforeAll` environment file, and - most importantly - that two concurrently running scenarios were
  handed *different* accounts (`standard_user` and `performance_glitch_user`, on different pool threads),
  recorded as Allure parameters on their respective results.

  Not verified locally: the browser-driving steps themselves. The authoring sandbox can reach neither
  saucedemo.com nor a version-matched Chrome/ChromeDriver pair, so that parallel run got as far as
  Selenium Manager failing to fetch a driver. CI is the first place the page objects and assertions run
  against the real site, the same caveat the sibling frameworks' READMEs record.
