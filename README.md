# gold-ui-test-automation

A gallery of sample UI test-automation frameworks, each built with a different technology stack,
so the same style of coverage can be compared side by side. Every framework targets the same demo
application ([saucedemo.com](https://www.saucedemo.com/)) wherever practical, so the stacks stay
apples-to-apples.

## Frameworks

| Folder | Language | Runner | BDD | Reporting | CI | Status |
|---|---|---|---|---|---|---|
| [`csharp-selenium-nunit-reqnroll/`](csharp-selenium-nunit-reqnroll/) | C# | NUnit (parallel) | Reqnroll | Allure | GitHub Actions + Jenkinsfile | Active |
| [`csharp-playwright-nunit-reqnroll/`](csharp-playwright-nunit-reqnroll/) | C# | NUnit (parallel) | Reqnroll | Allure | GitHub Actions + Jenkinsfile | Active |
| [`ts-playwright-cucumber/`](ts-playwright-cucumber/) | TypeScript | Cucumber.js (parallel) | Cucumber.js | Allure | GitHub Actions + Jenkinsfile | Active |
| [`python-selenium-pytest-bdd/`](python-selenium-pytest-bdd/) | Python | pytest + pytest-xdist (parallel) | pytest-bdd | Allure | GitHub Actions + Jenkinsfile | Active |
| [`java-selenium-junit5-cucumber/`](java-selenium-junit5-cucumber/) | Java | JUnit Platform (parallel) | Cucumber-JVM | Allure | GitHub Actions + Jenkinsfile | Active |

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
