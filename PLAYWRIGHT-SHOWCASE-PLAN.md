# Implementation plan - "Playwright Automation Testing Roadmap 2026" as working examples

This plan takes the twelve-section Playwright roadmap infographic and turns each item on it into a
concrete, runnable example in this repository: what gets built, where it lives, which application it
runs against, and what it actually proves.

Companion to [`ROADMAP.md`](ROADMAP.md), which covers the wider multi-stack gallery. This document is
Playwright-specific and item-by-item.

> **Status: phases 1, 2 and 6 are built.**
> [`ts-playwright-test/`](ts-playwright-test/) covers the section 2, 4-core, 5-most, 8 and 9-HTML items;
> [`ts-playwright-showcase/`](ts-playwright-showcase/) covers the rest of sections 4 and 5 (frames,
> alerts, uploads, downloads, multiple tabs, network interception, API mocking, mobile emulation) against
> an in-repo fixture app. Everything else on this page is still a plan.
>
> Phase 6 (structured logging + the failure taxonomy) landed in
> [`ts-playwright-test/`](ts-playwright-test/), closing the section 8 gaps.
>
> One correction from building it: this document originally proposed mocking saucedemo's *inventory
> response*. That is not possible - saucedemo bakes its product list into its bundle, so there is no
> request to intercept. The mocking examples run against the fixture app's own API instead.
>
> **Why phase 6 came before phases 3-5.** Phase 3 (`docker/`) exists to unlock phase 4 (the Toolshop API
> layer), and neither can currently be verified: the development sandbox has the Docker CLI and compose
> plugin but no daemon, and Toolshop's hosted API is blocked by the same network policy that blocks
> saucedemo. `docker compose config` validates schema without a daemon, but nothing can be built or run.
> Committing unrunnable infrastructure would contradict the standard the rest of this repository holds
> itself to, so the fully verifiable phase 6 was taken first. Phases 3-5 remain next in line, and want
> an environment with a working Docker daemon.

## Two findings that shape the whole plan

**1. About a third of the infographic is curriculum, not features.** Sections 1 (Software Testing
Foundations), 3 (JavaScript + TypeScript) and 11 (Strings & Data Structures) are prerequisite
knowledge. Writing toy `for`-loop or `reduce()` demos into a professional test repository would make
it look worse, not better. Those sections are handled differently: by a mapping table that points at
where each concept is already exercised in real framework code. That is honest and more convincing
than an exercise folder.

**2. saucedemo.com cannot demonstrate most of sections 4, 5 and 6.** It has no iframes, no native
alerts, no file upload or download, no second-tab flow, and no public API. Any plan that claims to
demo Frames/Alerts/Uploads/Downloads/Multiple Tabs/REST/GraphQL against saucedemo is fiction. Three
targets are therefore needed:

| Target | Used for | Why |
|---|---|---|
| **saucedemo.com** | the comparable UI suites | keeps the existing four frameworks apples-to-apples |
| **Toolshop** ([practice-software-testing](https://github.com/testsmith-io/practice-software-testing)) | REST, CRUD, bearer tokens, contract testing, UI+API | dockerised, OpenAPI 3.0 documented, seeded accounts, buggy variant for negative cases |
| **local fixture app** (new, in-repo) | frames, alerts, uploads, downloads, multi-tab, network states | deterministic and offline; served by Playwright's own `webServer`, which is itself a feature worth showing |

## Proposed structure

Three new top-level folders plus shared infrastructure, all following the existing repo conventions
(self-contained, own README with a *Known limitations* section, own path-filtered workflow):

```
ts-playwright-test/       comparison entry - same 3 saucedemo features, native Playwright Test
ts-playwright-bdd/        comparison entry - same 3 saucedemo features, playwright-bdd
ts-playwright-showcase/   capability catalog - one worked example per roadmap item
  fixtures-app/             tiny static app: iframes, alerts, uploads, downloads, popups
  mechanics/                core + advanced Playwright mechanics
  api/                      Toolshop REST, GraphQL, OAuth, contract testing
  ai/                       MCP, self-healing, measured experiment
docker/                   compose stacks, per-framework Dockerfiles, k8s manifests
```

`ts-playwright-showcase/` is deliberately **not** a comparison entry - its purpose is breadth, not
apples-to-apples. The root README table should mark it `Catalog` rather than `Active` so the
distinction is explicit.

---

## Section 1 - Software Testing Foundations

*SDLC · STLC · Agile · DevOps · Manual Testing · Automation Testing · SDET Career Path*

**Nothing to implement.** This is career and process knowledge with no code artifact.

The repository already demonstrates the parts of STLC that *do* have artifacts, and they are worth
pointing at rather than duplicating: defect classification (`categories.json` in all four
frameworks), the `@known-issue` tag applied to real, documented product defects, and the
*Known limitations* register in every README. One paragraph in the root README linking these is the
whole deliverable.

**Effort: S (documentation only).**

---

## Section 2 - Playwright Ecosystem

*Chrome · Firefox · WebKit · JS · TS · Installation · CLI · Codegen · Inspector · Trace Viewer*

| Item | Status | Lands in | Build note |
|---|---|---|---|
| Chrome / Firefox / WebKit | code supports, CI runs Chromium only | `ts-playwright-test/playwright.config.ts` | `projects` for chromium/firefox/webkit; full matrix nightly, chromium-only on PRs to keep feedback fast |
| JS / TS | TS covered | - | already strict-typed across the repo |
| Installation | covered | - | existing workflows cache `~/.cache/ms-playwright` and pin the version |
| CLI | partial | README | document `--ui`, `--debug`, `--last-failed`, `--repeat-each`, `--grep` |
| Codegen | missing | `ts-playwright-showcase/` README | record the `npx playwright codegen` workflow and, importantly, **what to fix in generated code** before committing it - generated locators are the classic maintenance debt |
| Inspector | missing | README | `PWDEBUG=1` walkthrough |
| Trace Viewer | missing | `ts-playwright-test/` | `trace: 'on-first-retry'`, traces uploaded as CI artifacts, README showing how to open one |

Trace-on-retry is the highest-value item here: the existing frameworks can only offer a screenshot
when CI fails, and a trace is strictly better evidence.

**Effort: S-M.**

---

## Section 3 - JavaScript + TypeScript

*Variables · Loops · Functions · OOP · Async/Await · Promises · Types · Interfaces · Generics · Access Modifiers · Best Practices*

**Nothing to implement as standalone examples.** Instead, a mapping table in
`ts-playwright-showcase/README.md` pointing each concept at real code:

| Concept | Where it is genuinely exercised |
|---|---|
| OOP / inheritance | `BasePage` → page objects, across all frameworks |
| Access modifiers | `private readonly` locators; page state exposed only through intent-revealing methods |
| Async/await, Promises | every page action; `Promise.all` for the click-plus-navigation pattern |
| Interfaces | the page contract each POM satisfies; `IUserPool` in the C# siblings |
| Generics | typed Playwright fixtures (`test.extend<Fixtures>`), typed config binding |
| Best practices | the ESLint/Prettier/`tsc --noEmit` gates already enforced in CI |

**Effort: S (documentation only).**

---

## Section 4 - Playwright Core Concepts

*Browser Context · Pages · Locators · Auto Wait · Assertions · Frames · Alerts · Uploads · Downloads · Screenshots · Multiple Tabs*

Split by what saucedemo can and cannot show.

**Against saucedemo, in `ts-playwright-test/`:**

| Item | Build note |
|---|---|
| Browser Context | one isolated context per worker, replacing the Cucumber `World` pattern |
| Pages | existing POM ported to Playwright Test fixtures |
| Locators | migrate the XPath-by-visible-text locators in `inventoryPage.ts` to `getByRole` / `getByTestId`; a side-by-side README note on why is a real teaching moment |
| Auto Wait | delete the `clickAndConfirmToggle` retry helper the Cucumber sibling needs and show web-first assertions doing the same job |
| Assertions | `expect(locator).toHaveText()` etc. - retrying by default |
| Screenshots | `screenshot: 'only-on-failure'` |

**Against the local fixture app, in `ts-playwright-showcase/mechanics/`:**

| Item | Fixture page needed |
|---|---|
| Frames | nested iframe with a form inside; `frameLocator()` |
| Alerts | `alert` / `confirm` / `prompt`; `page.on('dialog')` |
| Uploads | single + multiple file input; `setInputFiles()`, including the in-memory buffer variant |
| Downloads | link serving a generated CSV; `waitForEvent('download')` + `saveAs()` |
| Multiple Tabs | `target="_blank"` link and a `window.open` popup; `context.waitForEvent('page')` |

The fixture app is ~6 static HTML pages served by `playwright.config.ts`'s `webServer` block. No
framework, no build step, no third-party uptime dependency.

**Effort: M** (fixture app S, migration M).

---

## Section 5 - Advanced Playwright

*Parallel Execution · Fixtures · Hooks · Parameterization · Retry Logic · Debugging · Trace Viewer · Network Interception · API Mocking · Mobile Emulation*

| Item | Lands in | Build note |
|---|---|---|
| Parallel Execution | `ts-playwright-test/` | `fullyParallel` + workers; the existing 3-account user pool becomes a `worker`-scoped fixture - a much cleaner expression of it than the current lease object |
| Fixtures | `ts-playwright-test/` | `test.extend` for page objects, the authenticated session, and the assigned user |
| Hooks | `ts-playwright-test/` | `beforeEach`/`afterEach` plus auto-fixtures; README contrast with Cucumber's `Before`/`After` |
| Parameterization | `ts-playwright-test/` | data-driven loop replacing the Gherkin `Scenario Outline`, same cases |
| Retry Logic | `ts-playwright-test/` | `retries: 2` in CI only, with a flaky-test report section |
| Debugging | README | UI mode, `--debug`, `page.pause()` |
| Trace Viewer | `ts-playwright-test/` | see section 2 |
| Network Interception | `showcase/mechanics/` | `page.route` against saucedemo: assert the app's request payloads, and log the request/response waterfall |
| API Mocking | `showcase/mechanics/` | mock the inventory response to force **empty catalog**, **500 error** and **slow network** states - these are states the real app cannot produce, so this covers UI paths no existing test in the repo can reach |
| Mobile Emulation | `showcase/mechanics/` | `devices['iPhone 15']` / `['Pixel 7']` project; genuine device emulation, not a resized window - the README should say plainly that this is not a substitute for real-device testing |

API mocking is the standout item: it is the cheapest way to add negative-path UI coverage, and this
repo currently has none of it.

**Effort: M.**

---

## Section 6 - API Testing

*REST APIs · CRUD · OAuth · Bearer Tokens · GraphQL · Contract Testing · API Assertions*

All of this lands in `ts-playwright-showcase/api/`, against Toolshop.

| Item | Build note |
|---|---|
| REST APIs | `APIRequestContext` via the `request` fixture; no extra HTTP client dependency |
| CRUD | full product lifecycle create → read → update → delete against Toolshop, with cleanup |
| Bearer Tokens | Toolshop login returns a JWT; token acquired once in a worker fixture and reused |
| OAuth | Toolshop has no OAuth provider. Add [`navikt/mock-oauth2-server`](https://github.com/navikt/mock-oauth2-server) to the compose stack and run a real authorization-code flow against it, rather than faking one |
| GraphQL | Toolshop is REST-only. Two examples: a live query against a public GraphQL API, plus a **mocked** GraphQL endpoint via `page.route` so the example still passes offline and in CI |
| Contract Testing | Pact consumer test publishing the expectations the Toolshop UI has of the API, plus provider verification in CI. Depends on the REST work landing first |
| API Assertions | schema validation against Toolshop's OpenAPI 3.0 spec (Ajv), plus status/header/body assertions |

**Also worth building here, and arguably the most practically useful thing on this page:**
API-seeded UI setup. Log in and populate the cart over HTTP, save `storageState`, hand the ready
session to a UI test. This is the technique that makes real suites fast, and no framework in this
repository currently shows it.

**Effort: M-L** (contract testing is most of the L).

---

## Section 7 - BDD Framework

*Gherkin · Feature Files · Step Definitions · Hooks · Tags · Scenario Outline · playwright-bdd*

Six of the seven items are **already covered** by `ts-playwright-cucumber/` and the two Reqnroll
frameworks. The only gap is `playwright-bdd` itself - and it is the interesting one, because it is
the 2026 consensus answer for teams that want Gherkin without giving up Playwright's runner. It is
actively maintained and now even ships its own agent skill for generating features and steps.

Build `ts-playwright-bdd/`: the same three saucedemo features, `bddgen` generating spec files that
Playwright Test then runs - so the suite keeps fixtures, projects, traces, sharding, retries and the
HTML reporter that the Cucumber.js sibling gives up.

This produces a genuine three-way comparison on identical scenarios: **Cucumber.js** vs
**playwright-bdd** vs **plain Playwright Test**. That comparison is the single most defensible reason
to add it, and it is something the gallery is uniquely well-placed to show.

**Effort: M.**

---

## Section 8 - Framework Architecture

*Page Object Model · Design Patterns · Utilities · Logging · Config Management · Exception Handling · Enterprise Framework layout*

Mostly covered already, across four frameworks. Real gaps:

| Item | Status | Build note |
|---|---|---|
| POM | covered | `BasePage` + per-page objects everywhere |
| Design Patterns | covered, undocumented | add a short README section naming what is already there: POM, factory (`WebDriverFactory`, `PlaywrightFactory`), object pool + lease (`UserPool`), builder (data factories) |
| Utilities | covered | screenshot/attachment/environment helpers |
| Config Management | covered | layered `appsettings.json` → `.ci.json` → env |
| Exception Handling | partial | a typed error taxonomy distinguishing *product defect* / *environment* / *test bug*, wired to `categories.json` |
| **Logging** | **missing entirely** | structured per-test logger, correlated by test ID, attached to the Allure and HTML reports. Nothing in this repo logs anything today, which is the clearest architecture gap of the four |
| Enterprise layout | covered | `/pages /tests /utils /data /config` already matches |

**Effort: S-M.**

---

## Section 9 - Reporting & CI/CD

*HTML Reports · Allure Reports · GitHub Actions · Jenkins · Docker · Kubernetes · CI/CD Pipeline*

| Item | Status | Build note |
|---|---|---|
| Allure | covered, well | history/trends already published to `gh-pages` |
| GitHub Actions | covered, well | path-filtered, nightly canaries, staggered crons |
| Jenkins | covered | `Jenkinsfile` per framework |
| HTML Reports | missing | free with Playwright Test; publish alongside Allure and let the README compare them honestly |
| **Docker** | **missing** | `docker/`: a Dockerfile per framework (based on `mcr.microsoft.com/playwright`), a compose stack for Toolshop + mock-oauth2-server, and a Selenium Grid compose file for the three Selenium frameworks |
| **Kubernetes** | **missing** | a `Job` manifest running the suite with sharded parallelism across pods, plus a `kind` cluster job in CI so the manifest is actually verified rather than decorative |
| CI/CD Pipeline | partial | one end-to-end `code → test → build → deploy` example; the repo currently stops at `test` |

Docker is a prerequisite for the Toolshop-based work in section 6, so it should land before it.

**Effort: M** (Kubernetes is most of it).

---

## Section 10 - AI-Powered Testing

*MCP · Claude AI · AI Test Generation · AI Debugging · Self Healing · Playwright MCP · Future of Testing*

Nothing covered. This is the most current section and the easiest to do badly, so the plan is
deliberately conservative: build the tooling, then **measure it and publish the numbers**.

Lands in `ts-playwright-showcase/ai/`:

| Item | Build note |
|---|---|
| MCP / Playwright MCP | `.mcp.json` wiring the Playwright MCP server; README on driving exploratory sessions through it |
| AI Test Generation | a documented, reproducible workflow - agent explores a flow via MCP, drafts a spec, human reviews and commits. The review step is the point, not an afterthought |
| AI Debugging | worked example: feed a failing trace plus test source to an agent, capture what it correctly diagnosed and what it got wrong |
| Self Healing | a locator fixture that falls back through a candidate chain (`testid` → `role` → `text`) and **reports** every repair instead of silently passing |
| **Measurement** | the actual deliverable: deliberately break N locators, run the healer, publish repair rate, false-repair rate and added runtime |

Two rules for this folder, both stated in its README: AI-healed runs are **reported but never
merge-blocking**, and a silent self-heal is a defect in the harness, not a success - a repair that
nobody sees is a test that has quietly stopped testing what it claimed to.

Industry context worth citing in the write-up: Microsoft benchmarks put auto-repair above 75%, 61% of
organisations use AI somewhere in testing - but under 15% are expected to switch on genuinely agentic
features in 2026, and the senior-practitioner consensus is "constrained co-pilot", not autonomy.

**Effort: M.**

---

## Section 11 - Strings & Data Structures

*Strings · Arrays · Objects · JSON · Sets · Maps · map() · filter() · reduce()*

**Nothing to implement**, same treatment as section 3 - a mapping table to real usage:

| Structure | Where it is genuinely used |
|---|---|
| Set | the user pool's in-flight account tracking |
| Map | locator registries; per-worker session lookup |
| JSON | config layering; Allure result files; OpenAPI schema validation |
| `map` / `filter` / `reduce` | cart total arithmetic; product-name extraction from `allTextContents()` |

**Effort: S (documentation only).**

---

## Section 12 - Real-World Projects

*E-Commerce Framework · UI + API Framework · Dockerized Framework · Reporting · Parallel Execution · Environment Management · Enterprise Best Practices*

This section is an outcome of the others, not separate work:

| Item | Satisfied by |
|---|---|
| E-Commerce Framework | already done four times over (saucedemo) |
| UI + API Framework | section 6 (Toolshop, API-seeded UI setup) |
| Dockerized Framework | section 9 |
| Reporting | already done; plus HTML reports from section 9 |
| Parallel Execution | already done; plus fixtures-based rework in section 5 |
| Environment Management | already done (layered config); extend to a Toolshop `local`/`ci` split |
| Enterprise Best Practices | the *Known limitations* registers, static-analysis gates, and defect taxonomy |

**Effort: S** (integration and documentation only).

---

## Phasing

| Phase | Contents | Effort | Unlocks |
|---|---|---|---|
| **1 — done** | `ts-playwright-test/` - fixtures, projects, traces, HTML report, locator migration, browser matrix | M | sections 2, 4-core, 5-most, 8, 9-HTML |
| **2 — done** | fixture app + `showcase/mechanics/` - frames, alerts, uploads, downloads, tabs, network interception, API mocking, mobile emulation | M | rest of 4 and 5 |
| **3** | `docker/` - Dockerfiles, Toolshop + mock-oauth2 compose, Grid | M | required by phase 4 |
| **4** | `showcase/api/` - REST, CRUD, bearer, OAuth, GraphQL, schema assertions, API-seeded UI setup | M | section 6 minus Pact |
| **5** | `ts-playwright-bdd/` - the three-way BDD comparison | M | section 7 |
| **6 — done** | structured logging + error taxonomy | S-M | section 8 gaps |
| **7** | `showcase/ai/` - MCP, self-healing, the measured experiment | M | section 10 |
| **8** | Pact contract testing; Kubernetes `Job` + kind in CI | L | tail of 6 and 9 |
| **9** | the three documentation mappings (sections 1, 3, 11) and the section-12 integration pass | S | closes the roadmap |

Phases 1-2 alone cover roughly half the infographic and are the highest value per unit of effort.
Phase 8 is the only genuinely large item and can be deferred without leaving anything half-built.

## Risks and honest caveats

- **Third-party target drift.** saucedemo and the public GraphQL endpoint can change or go down. The
  existing nightly canary pattern covers saucedemo; the GraphQL example should ship with a mocked
  variant that always passes offline. Toolshop runs locally in Docker, so it carries no such risk.
- **Showcase folders rot faster than comparison folders.** A catalog of one-off examples has no
  natural pressure keeping it current. Every showcase example must run in CI, or it will be wrong
  within two Playwright releases.
- **Scope.** Twelve sections is a lot of surface. Landing phases 1-2 completely is worth far more
  than starting all nine.
- **The AI section will age fastest.** Dating the measurements and pinning tool versions in that
  README is not optional.
