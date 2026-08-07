# Roadmap - extending the gallery

A review of what this repository already demonstrates, measured against where the test-automation
market actually is in 2026, plus a prioritised list of what is worth adding next.

This document is a plan, not an implementation. Nothing below has been built yet.

## 1. What is here today

Four frameworks, all targeting [saucedemo.com](https://www.saucedemo.com/):

| Folder | Language | Driver | Runner | BDD | Reporting | CI |
|---|---|---|---|---|---|---|
| `csharp-selenium-nunit-reqnroll/` | C# | Selenium 4 | NUnit (parallel) | Reqnroll | Allure | GH Actions + Jenkins |
| `csharp-playwright-nunit-reqnroll/` | C# | Playwright | NUnit (parallel) | Reqnroll | Allure | GH Actions + Jenkins |
| `ts-playwright-cucumber/` | TypeScript | Playwright | Cucumber.js (parallel) | Cucumber.js | Allure | GH Actions + Jenkins |
| `python-selenium-pytest-bdd/` | Python | Selenium 4 | pytest + xdist | pytest-bdd | Allure | GH Actions + Jenkins |

### The shared baseline every framework already demonstrates

This is the part that is genuinely strong, and it is worth stating explicitly before listing gaps:

- **Page Object Model** with a shared `BasePage` and per-page locator ownership.
- **A user-pool / lease pattern** so parallel workers never share a saucedemo account - including
  unit tests for the pool logic itself, which is rare in sample frameworks.
- **Layered configuration**: `appsettings.json` -> `appsettings.ci.json` -> environment variables.
- **Test-data factories** (Bogus / Faker / `@faker-js/faker`) rather than hardcoded fixtures.
- **Failure diagnostics**: screenshot-on-failure, Allure attachments, environment metadata,
  `categories.json` defect classification.
- **Tag-driven selection**: `@smoke`, `@negative`, `@e2e`, `@known-issue`, `@user:<account>`.
- **Real CI**, not a token workflow: path-filtered triggers, nightly canary crons staggered across
  frameworks, Allure history/trend graphs published to `gh-pages`, single-file report artifacts,
  and a shared concurrency group so the four report jobs cannot race each other's `gh-pages` push.
- **Static-analysis gates** in the pipeline: `tsc` + ESLint + Prettier, `ruff` + `mypy` (strict).
- **Documented honesty**: each README carries a "Known limitations" section recording real defects
  and real flakiness found while building, rather than pretending the suite is green by nature.

The same three feature files (Login, Cart, Checkout - roughly 13 scenarios) are implemented in all
four stacks, which is what makes the gallery genuinely comparable.

## 2. Where the market is in 2026

The signals that matter for deciding what to add next:

- **Playwright has overtaken Selenium.** TestGuild's 2026 survey (40,000+ testers) puts Playwright
  at 45.1% adoption vs Selenium 22.1% and Cypress 14.4%; npm shows Playwright above 30M weekly
  downloads against `selenium-webdriver` under 2.1M. State of JS 2025 measured Playwright
  satisfaction at 91% vs Cypress 72%.
- **Selenium is not dead** - it holds on language breadth and on the large body of existing
  enterprise suites, which is exactly the work most job ads describe maintaining.
- **Java is the enterprise default this repo is missing.** Cucumber-JVM 7 with the JUnit 5 Platform
  Suite is the canonical enterprise BDD stack in 2026, and Polish job ads routinely list
  Java + Selenium + Playwright + SQL + Postman together.
- **Accessibility became a legal requirement in the EU.** The European Accessibility Act took
  effect across all 27 member states on 28 June 2025; sites serving EU users need WCAG 2.2 AA, and
  enforcement has started. axe-core catches roughly 30-57% of issues automatically - not complete,
  but the cheapest possible regression gate.
- **API-layer tests are the highest-ROI shift-left investment**, running 10-50x faster than the
  equivalent UI test. Contract testing (Pact) is now treated as essential past ~10 microservices.
- **Visual regression moved from "nice to have" to default** in mature pipelines, with Playwright's
  built-in screenshot comparison being the zero-cost entry point.
- **AI is broadly adopted but narrowly trusted.** 61% of organisations use AI somewhere in testing
  and 70% for test-case creation; Microsoft benchmarks put self-healing locator auto-repair above
  75%. But under 15% of firms are projected to switch on genuinely agentic features in 2026, and
  the senior-practitioner consensus is "constrained co-pilot", not autonomy.
- **Mobile**: Appium still leads enterprise adoption; Maestro is the fast-growing simpler challenger.

## 3. Gap analysis

### Stack coverage

|  | Selenium | Playwright | Native runner (no BDD) |
|---|---|---|---|
| **C#** | covered | covered | - |
| **TypeScript** | - | via Cucumber.js only | **missing** |
| **Python** | covered | **missing** | - |
| **Java** | **missing** | **missing** | - |

Two holes stand out: **Java is entirely absent**, and **Playwright's own test runner is never
shown** - the TypeScript framework drives Playwright through Cucumber.js, which deliberately
bypasses fixtures, projects, `storageState`, trace viewer, sharding, auto-retry, the HTML reporter,
`toHaveScreenshot`, and `APIRequestContext`.

### Capability coverage

Every framework here is a **pure functional UI suite**. Nothing in the repository covers:

| Capability | Status | Market pressure |
|---|---|---|
| Accessibility (axe-core) | absent | **High** - EAA legal requirement since 06/2025 |
| API testing | absent | **High** - top shift-left ROI |
| Visual regression | absent | **High** - now default practice |
| Cross-browser CI matrix | code supports it, CI runs Chromium only | Medium - near-zero cost to fix |
| Docker / Grid | Jenkins uses sidecars; no compose file in repo | Medium |
| Performance (k6) | absent | Medium |
| Contract testing (Pact) | absent | Medium - needs a microservice target |
| Mobile (Appium/Maestro) | absent | Medium |
| AI / MCP / self-healing | absent | Medium - strong differentiator |
| Cloud grid (BrowserStack etc.) | absent | Low-Medium |
| GitLab CI / Azure DevOps | absent (GHA + Jenkins only) | Low-Medium |

## 4. The target-application constraint

saucedemo.com is a front-end-only demo: there is no public REST API behind it. Anything at the API,
contract, or performance layer therefore needs a **second target application**.

Recommended: **[Toolshop / practice-software-testing](https://github.com/testsmith-io/practice-software-testing)**
- an Angular UI plus a documented Laravel REST API (OpenAPI 3.0, Swagger at
`api.practicesoftwaretesting.com/api/documentation`), fully dockerised, with seeded admin and
customer accounts and a deliberately buggy variant for negative testing. It gives UI + API + contract
+ performance work a single coherent target, and it can be run locally in CI rather than depending on
a third party's uptime.

saucedemo stays the target for everything UI-only, so the existing four frameworks remain
apples-to-apples.

## 5. Proposed additions, prioritised

### Tier 1 - biggest market gaps

**1. `ts-playwright-test/` - TypeScript + Playwright Test (no BDD)** *(effort: M)*

The single most in-demand web-testing stack of 2026, and the repo currently cannot show it. Same
saucedemo scenarios as the other four, so the comparison stays honest, but implemented the way
Playwright itself intends: custom fixtures instead of a Cucumber World, `projects` for the browser
matrix, `storageState` for API-seeded auth, trace-on-first-retry, sharding, and the built-in HTML
reporter alongside Allure. Directly contrastable with `ts-playwright-cucumber/` - same language,
same driver, same app, different philosophy - which is exactly the kind of comparison this gallery
exists to make.

This framework is also the natural host for items 3, 6 and 7 below.

**2. `java-selenium-junit5-cucumber/` - Java + Selenium 4 + JUnit 5 + Cucumber-JVM** *(effort: L)*

Closes the largest language gap. Java + Selenium + Cucumber-JVM remains the stack most enterprise
suites are actually written in and most job ads actually name, and the JUnit 5 Platform Suite runner
with parallel execution is the canonical 2026 configuration. Choosing Selenium over Playwright here
is deliberate: it makes a clean three-language Selenium comparison (C# / Python / Java) against the
existing siblings, and it matches where the maintenance work in the market really is. A
`java-playwright-junit5/` sibling is a reasonable later addition, not a first one.

Should reuse the established patterns: POM, the user-pool lease, Allure, the same three features.

**3. Accessibility gate with axe-core** *(effort: S)*

Added to `ts-playwright-test/` via `@axe-core/playwright`: a dedicated spec scanning login,
inventory, cart and checkout, with violations attached to the Allure report and a CI job that fails
on new serious/critical findings. Highest signal-to-effort ratio in this list given the EAA, and
saucedemo has genuine violations to find, so the demo is real rather than staged. The README should
state plainly that automated scanning covers only part of WCAG and does not equal compliance.

### Tier 2 - depth beyond the UI layer

**4. API test layer against Toolshop** *(effort: M)*

Two complementary pieces:
- `java-restassured-junit5/` - REST Assured is the enterprise API-testing standard and appears
  constantly in Java QA job ads. CRUD, auth, negative cases, schema validation against the OpenAPI spec.
- API-driven setup inside `ts-playwright-test/` - logging in and seeding state over
  `APIRequestContext`, then handing a ready session to the UI test. This is the practice that
  actually makes UI suites fast, and no framework here shows it.

**5. Docker Compose target + Selenium Grid** *(effort: M)*

A `docker/` directory bringing up Toolshop locally, plus a Grid compose file so the three Selenium
frameworks can run cross-browser without host-installed browsers. Removes the current dependency on
a third-party demo site staying up, and makes the Jenkins sidecar approach reproducible locally.

**6. Cross-browser CI matrix** *(effort: S)*

The code already supports Firefox and WebKit; CI runs Chromium only. Turning on a matrix in the two
Playwright frameworks costs almost nothing and immediately demonstrates something the repo currently
claims but never exercises. Worth scheduling the wider matrix nightly rather than per-PR to keep PR
feedback fast.

**7. Visual regression** *(effort: S-M)*

Playwright's `toHaveScreenshot` in `ts-playwright-test/`, with baselines committed per-platform, a
documented update workflow, and masking for the volatile parts of the page. Zero additional
dependency cost, and it covers a defect class the functional suites structurally cannot see.

**8. Performance smoke with k6** *(effort: S-M)*

A small k6 script against the Toolshop API with thresholds wired into a CI quality gate. Not a load
test - a regression gate. Demonstrates that performance belongs in the pipeline, not in a separate
end-of-project phase.

### Tier 3 - differentiators

**9. AI-assisted layer: Playwright MCP + self-healing experiment** *(effort: M)*

The most current thing on this list, and the one most easily done badly. Worth doing precisely
because an honest treatment is rare: wire up the Playwright MCP server for agent-driven exploration,
try a self-healing locator strategy, then **measure and publish what it actually does** - what
fraction of deliberately broken locators it repairs, what it gets wrong, and what it costs per run.
Written up in the same "Known limitations" register the rest of the repo already uses, this becomes
a credibility asset rather than a bandwagon entry.

**10. Contract testing with Pact** *(effort: L)*

Toolshop's UI-to-API boundary gives a real consumer/provider pair. Consumer tests publish
expectations, provider verification runs in CI. The most "senior" item here, and the least
meaningful without item 4 in place first.

**11. Mobile: Appium or Maestro** *(effort: L)*

Appium for enterprise alignment, Maestro for a fast readable demo. Largest infrastructure cost of
anything on this list (emulators in CI), so it should be last unless mobile is a specific target.

**12. Alternative CI providers** *(effort: S)*

GitLab CI and Azure DevOps pipeline definitions for one existing framework. Cheap breadth - many
organisations run neither GitHub Actions nor Jenkins, and the translation is mostly mechanical.

## 6. Suggested sequencing

1. **`ts-playwright-test/`** - biggest single gap, and it unlocks items 3, 6 and 7.
2. **axe-core gate** - small, and the strongest EU-market signal available.
3. **Cross-browser matrix + visual regression** - both small, both build on step 1.
4. **`java-selenium-junit5-cucumber/`** - largest effort, largest language gap; worth doing properly
   rather than quickly.
5. **Docker Compose + Toolshop**, then **API layer**, then **k6**.
6. **AI/MCP write-up** once there are enough tests for the measurement to mean anything.
7. **Pact**, **mobile**, **extra CI providers** as scope allows.

## 7. Conventions any addition should keep

- Self-contained top-level folder, `<language>-<key-libraries>/`, no code shared across frameworks.
- Its own `README.md` including a **Known limitations** section, matching the existing four.
- Its own path-filtered workflow at `.github/workflows/<folder-name>.yml`.
- The same three saucedemo features where the framework is UI-functional, so comparability holds.
- The same failure diagnostics: screenshots, Allure attachments, environment metadata.
- Static-analysis gates in CI, not just tests.

## Sources

- [Stack Overflow: Selenium vs Cypress vs Playwright](https://stackoverflow.blog/2026/06/15/selenium-vs-cypress-vs-playwright-choosing-your-test-automation-framework/)
- [Playwright vs Cypress vs Selenium download and adoption data](https://tech-insider.org/playwright-vs-cypress-vs-selenium-2026/)
- [Cucumber vs Playwright BDD in 2026](https://qaskills.sh/blog/cucumber-vs-playwright-2026)
- [Cucumber Java BDD best practices 2026](https://qaskills.sh/blog/cucumber-java-bdd-best-practices-2026)
- [axe vs WAVE vs Pa11y, accessibility tooling compared](https://crosscheck.cloud/blogs/axe-vs-wave-vs-pa11y-accessibility-testing/)
- [Accessibility testing tools 2026](https://testguild.com/accessibility-testing-tools-automation/)
- [API test automation best practices 2026](https://www.vervali.com/blog/api-test-automation-best-practices-2026-rest-graphql-grpc-ci-cd-and-contract-testing/)
- [Automation testing trends 2026](https://www.itconvergence.com/blog/automation-testing-trends-2026)
- [BrowserStack: State of AI in Software Testing 2026](https://www.browserstack.com/blog/inside-the-state-of-ai-in-software-testing-2026/)
- [Playwright AI ecosystem: MCP, agents, self-healing](https://testdino.com/blog/playwright-ai-ecosystem)
- [Appium market share and adoption 2026](https://testdino.com/blog/appium-market-share-2026/)
- [Toolshop / practice-software-testing](https://github.com/testsmith-io/practice-software-testing)
