# gold-ui-test-automation

A gallery of sample test-automation frameworks, each built with a different technology stack, so the
same style of coverage can be compared side by side. Most target the same demo application
([saucedemo.com](https://www.saucedemo.com/)) wherever practical, so the UI stacks stay
apples-to-apples; a couple deliberately step outside that to cover disciplines a UI demo cannot (see
the exceptions noted under the table).

## Frameworks

| Folder | Language | Runner | BDD | Reporting | CI | Status |
|---|---|---|---|---|---|---|
| [`csharp-selenium-nunit-reqnroll/`](csharp-selenium-nunit-reqnroll/) | C# | NUnit (parallel) | Reqnroll | Allure | GitHub Actions + Jenkinsfile | Active |
| [`csharp-playwright-nunit-reqnroll/`](csharp-playwright-nunit-reqnroll/) | C# | NUnit (parallel) | Reqnroll | Allure | GitHub Actions + Jenkinsfile | Active |
| [`ts-playwright-cucumber/`](ts-playwright-cucumber/) | TypeScript | Cucumber.js (parallel) | Cucumber.js | Allure | GitHub Actions + Jenkinsfile | Active |
| [`python-selenium-pytest-bdd/`](python-selenium-pytest-bdd/) | Python | pytest + pytest-xdist (parallel) | pytest-bdd | Allure | GitHub Actions + Jenkinsfile | Active |
| [`java-selenium-junit5-cucumber/`](java-selenium-junit5-cucumber/) | Java | JUnit Platform (parallel) | Cucumber-JVM | Allure | GitHub Actions + Jenkinsfile | Active |
| [`servicenow-atf/`](servicenow-atf/) | ATF (no-code) + Python glue | ServiceNow ATF in-instance, driven via the CI/CD REST API | — | JUnit in CI + native ATF results in-instance | GitHub Actions + Jenkinsfile | Active |
| [`python-pyspark-pytest-dataquality/`](python-pyspark-pytest-dataquality/) | Python | pytest (single-process; Spark parallelizes internally) | — | Allure | GitHub Actions + Jenkinsfile | Active |
| [`csharp-backend-xunit-testcontainers/`](csharp-backend-xunit-testcontainers/) | C# | xUnit + NUnit | — | TRX (dorny/test-reporter) | GitHub Actions + Jenkinsfile | Active |

Three frameworks deliberately step outside the saucedemo/UI convention, because their whole point is a
discipline a web UI demo cannot represent:

- `servicenow-atf/` - ATF tests are records that execute *inside* a ServiceNow instance (a free
  Personal Developer Instance), so its system under test is the platform itself. See its
  [README](servicenow-atf/README.md) for how the specs-as-code + REST-triggered setup keeps it
  reviewable and CI-friendly anyway.
- `python-pyspark-pytest-dataquality/` - a **Big Data / data-quality** framework: PySpark + pytest
  testing a Databricks-style medallion (bronze→silver→gold) lakehouse pipeline, with a reusable
  data-quality check library, cross-layer Spark SQL reconciliation, and metamorphic tests for an
  AI-style enrichment step. Its system under test is a data pipeline, not a screen. It is fully
  self-contained (a bundled sample dataset, no external service), so unlike the UI frameworks its whole
  suite runs and is verified locally. See its [README](python-pyspark-pytest-dataquality/README.md).
- `csharp-backend-xunit-testcontainers/` - a **backend unit + integration** framework: an ASP.NET Core
  API on EF Core, tested with xUnit and NUnit, NSubstitute for isolation, and Testcontainers (a real
  SQL Server in a container) + Respawn + `WebApplicationFactory` for full-stack integration tests. Its
  system under test is an HTTP+database service, not a screen. See its
  [README](csharp-backend-xunit-testcontainers/README.md).

## What to add next

[`ROADMAP.md`](ROADMAP.md) reviews the current coverage against where the test-automation market is
in 2026, and lists prioritised candidates for the next additions - the two largest gaps being Java
and Playwright's own test runner, plus capabilities no framework here covers yet (accessibility,
API, visual regression).

[`PLAYWRIGHT-SHOWCASE-PLAN.md`](PLAYWRIGHT-SHOWCASE-PLAN.md) is the Playwright-specific companion: an
item-by-item plan for turning the twelve-section "Playwright Automation Testing Roadmap 2026" into
runnable examples, including which target application each one needs.

## Conventions for adding a new framework

Each framework is fully self-contained under its own top-level folder:

- Its own solution/build/project files - no code sharing across frameworks.
- Its own `README.md` documenting how to run it locally and in CI.
- Its own CI workflow under `.github/workflows/<framework-folder-name>.yml`, path-filtered to that
  framework's folder (plus the workflow file itself) so unrelated frameworks never trigger each
  other's pipeline.
- Prefer targeting the same demo application other frameworks already use, so the stacks are
  comparable. `saucedemo.com` is the default choice: it's public, stable, and ships several distinct
  login users - handy for demonstrating parallel execution without a real backend.

Folder naming: `<language>-<key-libraries>/`, e.g. `csharp-selenium-nunit-reqnroll`,
`java-selenium-junit5-cucumber`, `ts-playwright-cucumber`, `python-selenium-pytest-bdd`.

## License

[MIT](LICENSE)
