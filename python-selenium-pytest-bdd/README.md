# python-selenium-pytest-bdd

[![CI](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/python-selenium-pytest-bdd.yml/badge.svg)](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/python-selenium-pytest-bdd.yml)

Selenium UI tests for [saucedemo.com](https://www.saucedemo.com/), written in Python with
[pytest](https://docs.pytest.org/) as the runner, [pytest-bdd](https://pytest-bdd.readthedocs.io/) for
Gherkin BDD, [Allure](https://allurereport.org/) for reporting, and [Faker](https://faker.readthedocs.io/)
for fake test data. This is the fourth framework in this repo's gallery, and the first built on a
fixture-based BDD tool rather than a Given/When/Then class-binding one - see
["How this differs from the other siblings"](#how-this-differs-from-the-other-siblings) for what that
actually changes. Scenarios run **in parallel**, and each concurrently-running scenario logs in with its
**own, distinct saucedemo user** - no two parallel sessions ever share an account.

## Stack

| Concern | Choice |
|---|---|
| Language | Python 3.11+ |
| Browser automation | Selenium WebDriver 4 (Selenium Manager resolves ChromeDriver automatically - nothing to install) |
| Test runner | pytest 9, parallelised with pytest-xdist |
| BDD | pytest-bdd (Gherkin `.feature` files + fixture-based step bindings) |
| Test data | Faker (checkout form data only - login users are fixed, real accounts) |
| Reporting | Allure (via `allure-pytest`) |
| Package management | [uv](https://docs.astral.sh/uv/) |
| Lint / format | [ruff](https://docs.astral.sh/ruff/) |
| Type checking | mypy (`strict = true`) |
| CI | GitHub Actions (primary) + Jenkinsfile (secondary) |

## Prerequisites

- Python 3.11+
- [uv](https://docs.astral.sh/uv/getting-started/installation/)
- A Chrome install (Selenium Manager downloads a matching ChromeDriver on first run and caches it
  under `~/.cache/selenium`)
- Optional, for viewing reports locally: the [Allure commandline](https://allurereport.org/docs/gettingstarted-installation/)
  (`npm i -g allure-commandline`, `brew install allure`, or `scoop install allure`)

## Quick start

```bash
cd python-selenium-pytest-bdd
uv sync
scripts/run-tests.sh            # or scripts\run-tests.ps1 on Windows
scripts/generate-report.sh      # opens the Allure report in a browser
```

Other useful commands: `uv run ruff check .`, `uv run ruff format --check .`, `uv run mypy .`,
`uv run pytest tests/unit` (the browser-independent unit tests), `uv run pytest --collect-only` (verifies
every step binds without launching a browser - the pytest-bdd equivalent of cucumber-js's `--dry-run`).

## Running in CI

- **GitHub Actions**: [`.github/workflows/python-selenium-pytest-bdd.yml`](../.github/workflows/python-selenium-pytest-bdd.yml),
  triggered on push/PR to this folder, `workflow_dispatch` (custom worker count / marker filter), and a
  nightly schedule. Publishes an Allure report to GitHub Pages under `python-selenium/<run-number>/` and
  `python-selenium/latest/` (namespaced so it never collides with any other framework's own reports on
  the same `gh-pages` branch).
- **Jenkins**: [`Jenkinsfile`](Jenkinsfile) - same Docker-sidecar approach as the C# Selenium sibling
  (a `selenium/standalone-chrome` container reached over `REMOTE_URL`), since a plain `python:3.11-slim`
  agent image has no browser of its own either.

Both always run headless and with 3 parallel workers by default (`appsettings.ci.json` forces
`Headless: true` whenever the standard `CI=true` env var is present).

## Configuration

`appsettings.json` (defaults) + `appsettings.ci.json` (overrides layered in when `CI=true`) + plain env
vars (highest precedence). See [`src/saucedemo_uitests/config/settings.py`](src/saucedemo_uitests/config/settings.py):

| Setting | Env var override | Default | Notes |
|---|---|---|---|
| `BaseUrl` | `BASE_URL` | `https://www.saucedemo.com/` | |
| `Browser` | `BROWSER` | `chrome` | `chrome`, `firefox`, or `edge` |
| `Headless` | `HEADLESS` | `false` (CI: `true`) | |
| `RemoteUrl` | `REMOTE_URL` | *(none)* | Selenium Grid / remote endpoint, e.g. `http://localhost:4444/wd/hub`. Set to run against a container instead of a local browser - no code changes needed. |
| `ExplicitWaitSeconds` | `EXPLICIT_WAIT_SECONDS` | `20` (CI: `30`) | Explicit wait used by every page-object interaction |
| `PageLoadTimeoutSeconds` | `PAGE_LOAD_TIMEOUT_SECONDS` | `30` (CI: `45`) | Page-load timeout |

Like the TS sibling, env vars here are plain and flat (`BASE_URL`, not `TestSettings__BaseUrl`) rather
than mirroring the C# siblings' .NET-style nested-config convention literally - that convention is
idiomatic there and would just look foreign in Python. The JSON file *shape* (`{"TestSettings": {...}}`)
is kept identical across all four frameworks on purpose, since that's a comparable data format, not a
language idiom.

## Project structure

```
python-selenium-pytest-bdd/
├── pyproject.toml / uv.lock                 # deps, ruff/mypy/pytest config
├── conftest.py                              # step-module imports + driver/user fixtures (see below)
├── Jenkinsfile                              # secondary CI
├── scripts/                                 # run-tests / generate-report (sh + ps1)
├── appsettings.json / appsettings.ci.json / categories.json
├── features/                                # login.feature, cart.feature, checkout.feature
├── src/saucedemo_uitests/
│   ├── config/          settings.py, parallel_settings.py
│   ├── drivers/          driver_session.py - WebDriver session factory (Selenium Manager, headless, remote)
│   ├── users/            user_account, user_catalog, assigned_user, tag_helpers - the per-worker user mechanism
│   ├── pages/            Page objects (base_page + one per saucedemo page)
│   ├── support/           screenshot/Allure-attachment/environment.properties helpers, the polling assertion helper
│   ├── steps/             pytest-bdd step bindings, one module per feature's domain
│   └── testdata/          Faker factories + fixed product catalog
└── tests/
    ├── bdd/                One thin module per feature, each just calling `scenarios("<name>.feature")`
    └── unit/               Plain pytest unit tests for user assignment (no browser)
```

## Parallel execution & user assignment

pytest-xdist's `-n 3` runs 3 **separate OS processes**, each a full, independent pytest session with its
own `PYTEST_XDIST_WORKER` env var (`"gw0"`, `"gw1"`, `"gw2"`, ...) for its whole lifetime. Unlike the C#
siblings' NUnit threads (needing a `BlockingCollection`-based lease/release pool to stop two threads
racing for the same account) or even the TS sibling's `worker_threads` (isolated V8 contexts, but still
one process), separate xdist processes have no shared memory to race over in the first place - so
[`users/assigned_user.py`](src/saucedemo_uitests/users/assigned_user.py) just maps a worker's numeric
index onto one of the three checkout-capable accounts, computed once, with no locking or release step at
all:

```
gw0 ──▶ get_assigned_user() ──▶ standard_user             ──▶ own Chrome session
gw1 ──▶ get_assigned_user() ──▶ performance_glitch_user   ──▶ own Chrome session
gw2 ──▶ get_assigned_user() ──▶ visual_user               ──▶ own Chrome session
```

A scenario tagged `@user:<username>` (e.g. `@user:locked_out_user`) bypasses that assignment entirely to
target one specific account directly - see [`users/tag_helpers.py`](src/saucedemo_uitests/users/tag_helpers.py).
The `driver` fixture in [`conftest.py`](conftest.py) is function-scoped (a fresh browser **per scenario**,
matching both C# siblings - xdist processes have no browser-per-worker optimisation to make, unlike the
TS sibling's worker-shared browser).

### The saucedemo user roster

Only 3 of saucedemo's 6 accounts can complete a full purchase, so only those 3 are in the parallel pool
(`users/user_catalog.py`'s `POOL_USERS`, derived automatically from the `can_complete_checkout` flag below
rather than listed by hand). The other 3 are deliberately broken and are instead targeted directly by
scenarios tagged `@user:<username>`, bypassing the pool entirely:

| User | Login | Full checkout | Notes |
|---|---|---|---|
| `standard_user` | ✅ | ✅ | baseline - in the pool |
| `performance_glitch_user` | ✅ | ✅ | ~5s artificial delays - in the pool |
| `visual_user` | ✅ | ✅ | cosmetic-only defects - in the pool |
| `problem_user` | ✅ | ❌ | checkout last-name field is broken - targeted via `@user:problem_user` |
| `error_user` | ✅ | ❌ | not currently targeted by a scenario (the cart-removal scenario that used to exercise it never passed reliably against the live site, so it was removed as flaky; kept in the roster for completeness) |
| `locked_out_user` | ❌ | ❌ | login rejected by design - targeted via `@user:locked_out_user` |

Raising the worker count above 3 without adding more accounts to `POOL_USERS` makes two workers wrap
around onto the same account (`assigned_user.py`'s index is taken modulo the pool size), which is exactly
what the CI workflow's "validate worker count" step exists to reject early.

## Allure reporting

`environment.properties`, `categories.json`, and failure-evidence attachments (screenshot, page HTML,
final URL, browser console log where the driver supports it) work the same way as the other three
frameworks - see [`support/`](src/saucedemo_uitests/support/). One cosmetic, verified difference:
`allure-pytest`'s own `represent()` utility wraps every string parameter value in an extra pair of single
quotes before display (confirmed directly from `allure_commons.utils.represent`'s own doctests - this is
true for *every* allure-pytest user's string parameters, not specific to this project), so the `user`
parameter shows as `'standard_user'` rather than the plain `standard_user` the C#/TS reports display.

## How this differs from the other siblings

Porting scenario-for-scenario onto a fixture-based BDD tool - not just a different browser library or
parallelism model this time - surfaced several places where the right design isn't "the same shape with
different syntax". Every claim below was checked directly (reading the installed library's own source, or
running a small real script against it) rather than assumed from documentation or general familiarity
with similarly-named concepts in the other three frameworks:

- **pytest-bdd has no shared mutable "World"/context object.** The C# siblings register `IWebDriver`/
  `UserAccount` into a per-scenario DI container; the TS sibling has a custom Cucumber `World`. Here,
  state is just pytest fixtures: steps that need the browser or the current user declare a `driver`/
  `user` parameter and pytest resolves it, the same mechanism any other pytest test uses.
- **A step module must be `import *`-ed into `conftest.py` to be globally visible - a plain `import` does
  not work.** Confirmed directly: pytest-bdd resolves a step by scanning the *defining* module's own
  namespace for `@given`/`@when`/`@then`-decorated functions. Importing a steps module some other way
  (`import pkg.steps` or `from pkg import steps`) leaves those functions sitting in `pkg.steps`'s
  namespace, invisible to pytest's fixture collection; only binding the names directly into `conftest.py`
  via `from pkg.steps import *` actually registers them. See `conftest.py`'s own comment.
- **The `parse` library's default `{}` placeholder requires at least one character - it will not match
  an empty string.** `Login.feature`'s invalid-credentials Outline has rows with a genuinely blank
  username or password (`I log in with username "" and password "secret_sauce"`), and `parsers.parse`
  returns no match at all for those - confirmed by trying it directly. Cucumber Expressions' `{string}`
  (used by the C#/TS siblings' equivalent step) has no such gap. `steps/login_steps.py` uses
  `parsers.re` with an explicit `(?P<name>.*)` group for that one step instead, which does match empty
  captures; every other quoted-string step in this suite only ever receives non-empty values in practice,
  so `parsers.parse` is used there for readability.
- **`WebDriverWait.until`'s type signature (`Literal[False] | T`) doesn't know that `None` is treated
  identically to `False` at runtime.** Its actual loop is a plain `if value:` truthiness check (confirmed
  from the installed selenium source - `None` and `False` are both simply falsy to it), but mypy can only
  narrow `T` correctly if the predicate's return type is literally `Literal[False] | WebElement`, not
  `WebElement | None` - so `base_page.py`'s wait-predicates return `False`, never `None`, purely so mypy
  infers the right return type for `wait_for_visible`/`wait_for_clickable`.
- **A `TypeGuard` return type narrows correctly in the negative branch too.** `config/settings.py`'s
  `_is_browser_name` returns `TypeGuard[BrowserName]`; after `if not _is_browser_name(browser): raise
  ...`, mypy narrows `browser` to `BrowserName` in the rest of the function - confirmed with a standalone
  mypy check before relying on it, since `TypeGuard` (PEP 647) is sometimes described as one-directional
  (true-branch only) as opposed to the stricter `TypeIs` (PEP 742).
- **pytest's own `filterwarnings` ini entries split the whole string on every literal `:`, with no
  escaping** (confirmed from `_pytest.config.parse_warning_filter`'s `arg.split(":")`). The `@user:<name>`
  tags this suite relies on produce warning text containing a literal `:`, which broke the naive
  `ignore:Unknown pytest\.mark\.user:.*:pytest.PytestUnknownMarkWarning` entry (the embedded `:` shifted
  every later field over by one, misparsing `pytest.PytestUnknownMarkWarning` as part of the message).
  Fixed by using `.` (matches any one character, including a colon) in place of the literal `:` - the
  message only needs to match as a *prefix* of the real warning anyway, since filter matching is
  `re.match`, not `re.fullmatch`. See `pyproject.toml`'s comment.
- **Faker has the same global-vs-instance seeding split as Bogus, under different names.** `Faker.seed(...)`
  is a classmethod that reseeds the one shared random generator every `Faker()` instance draws from -
  confirmed directly (`type(Faker.__dict__["seed"])` is `classmethod`) - the exact trap the C# siblings'
  own comments warn about for `Bogus.Randomizer.Seed`. `fake.seed_instance(...)` is the instance-level,
  safe equivalent, used by `testdata/checkout_data_factory.py`'s optional `seed` parameter.
- **`astral-sh/setup-uv` moved to immutable, SHA-pinned releases starting at v8.0.0** - floating tags like
  `@v8`/`@v9` no longer exist at all, unlike every other Actions dependency in this repo's workflows.
  Checked directly against the action's own releases page rather than guessed from a remembered version
  number, since guessing wrong here would have failed CI outright on the very first run. The workflow
  pins the exact commit SHA with a `# vX.Y.Z` comment, per the project's own current documentation.

## Known limitations

- **This sandbox cannot locally smoke-test a real browser launch at all**, which the three earlier
  frameworks in this gallery could each do in some form. The pre-installed `chromedriver` reports version
  147; the only browser binary available here (Playwright's bundled Chromium, installed for the two
  Playwright siblings) reports 141 - a real version mismatch, but Selenium Manager's own default browser
  discovery does not even get that far: with no `google-chrome`/`chromium` binary in a location Selenium
  Manager recognises, a plain `webdriver.Chrome()` call fails immediately with `NoSuchDriverException:
  Unable to obtain driver for chrome` (confirmed directly, including via this framework's own
  `scripts/run-tests.sh`). Explicitly wiring both `Service(executable_path=...)` and
  `options.binary_location` to those exact two mismatched paths does reproduce the underlying
  `SessionNotCreatedException: ChromeDriver only supports Chrome 147, current browser version is 141`
  - but neither path is something this framework's own code does, or should: the C# Selenium sibling
  relies on the same plain Selenium Manager auto-resolution, with no explicit-path override. Local
  verification therefore stops at `ruff`/`mypy`/`pytest --collect-only`/the unit tests; a real browser
  launch is verified by CI, which installs its own version-matched Chrome via `browser-actions/setup-chrome` -
  the same gate the C# Selenium sibling's own CI already is for it.
- **pytest-bdd 8.1.0 uses a pytest-9.x-deprecated internal fixture-registration API**, observed as a
  `PytestRemovedIn10Warning` during Scenario Outline verification. This is upstream pytest-bdd's own
  internals, not this project's code - worth knowing about if a future pytest 10 upgrade breaks
  Scenario Outline collection, not something to patch here.
- **The `@allure.label.severity:critical` tag on `Checkout.feature`'s smoke scenario was dropped when
  porting it**, same as the TS sibling's own precedent: `allure-pytest` has no automatic tag-to-severity
  convention (severity is set via `allure.dynamic.severity(...)`, never inferred from a Gherkin tag), and
  porting the tag literally would need yet another `filterwarnings` entry for the same colon-splitting
  reason described above.
- Only 3 saucedemo accounts can complete checkout, which caps meaningful in-pool parallelism at 3 - the
  same ceiling every sibling framework has, enforced at import time by `assigned_user.py`.
- `saucedemo.com` is a public demo site with no uptime SLA; the CI workflow's nightly schedule exists to
  catch drift before it blocks a PR.
- `firefox`/`edge` are supported by `drivers/driver_session.py` but only Chrome is installed in CI.
- **No automatic retry of failed scenarios**, by design: a real outage would otherwise be silently
  masked. `pytest-rerunfailures` would be the natural add-on if nightly-canary flakiness from transient
  network blips becomes a problem, scoped to the nightly `schedule` trigger only.

## Adding a scenario / a page object

1. Add or extend a `.feature` file under `features/`.
2. Add matching step functions to the relevant module under `src/saucedemo_uitests/steps/`, declaring
   `driver`/`user` as plain parameters (pytest resolves them as fixtures) and using `parsers.parse(...)`/
   `parsers.re(...)` for any parameterised step text.
3. Add a page object under `src/saucedemo_uitests/pages/` if the scenario touches a new page: extend
   `BasePage`, take only a `WebDriver` in the constructor, and use its `click`/`type_text`/`text_of`/
   `is_visible` helpers - never `time.sleep`.
4. **Verify every new locator against real page source before trusting it.** Don't assume `data-test`
   values or structure from documentation or general knowledge about the site; the C# Selenium sibling's
   README records more than one locator that looked plausible and was wrong.
5. Run `uv run pytest --collect-only` to confirm the new step binds before trying a real browser run.

## License

[MIT](../LICENSE)
