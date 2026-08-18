# csharp-backend-xunit-testcontainers

A sample **backend** test-automation framework: unit and integration tests for a small C# / ASP.NET
Core service, built to demonstrate one specific stack end to end —

> **xUnit · NUnit · NSubstitute · Testcontainers (MsSql) · Respawn**

Unlike the UI frameworks in this repo, the system under test here is an HTTP+database service, not a
screen, so there is no saucedemo/browser involved. The point is to show how the two layers of a backend
test pyramid are written and wired: fast, isolated **unit tests** over the business logic, and
**integration tests** that exercise the real thing against a real SQL Server.

## The system under test

`src/OrderService.Api` is a minimal ASP.NET Core API on EF Core 10 (SQL Server provider) — a tiny
order-service slice with just enough real behaviour to be worth testing:

- **Products** — create (`POST /products`), list, get by id. Creation validates the SKU format
  (`ABC-1234`), a positive price, non-negative stock, and SKU uniqueness.
- **Orders** — place (`POST /orders`), get by id. Placing an order validates the cart, checks stock,
  computes the total, **decrements product stock and inserts the order in one transaction**, and
  returns the created order.

The interesting invariant — the one that most justifies a real database — is that placing an order
both writes the order *and* moves stock atomically. A unit test can assert the rule; only an
integration test against a real provider proves the cross-row write actually commits together.

Domain failures are modelled as **result types** (`OrderResult` / `ProductResult` with an error enum),
not exceptions. That keeps the business rules trivially assertable in unit tests and maps cleanly to
`400 Bad Request` at the endpoint, with `Created` on success.

## The test pyramid, layer by layer

| Layer | Project | Runner | Isolation | What it proves |
|---|---|---|---|---|
| Unit — order placement | `tests/OrderService.UnitTests.Xunit` | **xUnit** | **NSubstitute** mocks for repos/UoW/clock | Every placement rule (empty cart, bad quantity, unknown product, insufficient stock, total & stock math, duplicate-line collapsing) in isolation, no database |
| Unit — product/SKU rules | `tests/OrderService.UnitTests.Nunit` | **NUnit** | **NSubstitute** mock repo | SKU-format validation and the product-creation rules (invalid SKU/price/stock, duplicate SKU) |
| Integration — full stack | `tests/OrderService.IntegrationTests` | **xUnit** | none — real API + real SQL Server | Real HTTP → minimal API → service → EF Core → **SQL Server in a container** and back |

### Why both xUnit *and* NUnit?

The stack lists both runners, so both are demonstrated — but on **genuinely different slices** rather
than duplicated. xUnit drives the order-placement logic and all the integration tests; NUnit drives the
product/SKU validation. You get to see each runner's idiom on real tests: xUnit's `[Fact]` / `[Theory]`
with `[InlineData]`, and NUnit's `[TestFixture]` / `[TestCase]` / `[TestCaseSource]` and
`Assert.That(..., Is.EqualTo(...))`.

### NSubstitute in the unit tests

The unit tests never touch a database. The repositories, unit of work and clock are
`Substitute.For<T>()` fakes, so the tests are fast and pin down exactly one thing — the rules. The
patterns on show:

- Stubbing return values — `_products.GetByIdsAsync(...).Returns(products)`, and an injected fixed
  clock (`_clock.UtcNow.Returns(FixedNow)`) for deterministic timestamps.
- Verifying interactions — `await _orders.Received(1).AddAsync(result.Order, Arg.Any<CancellationToken>())`
  and, on the failure paths, `DidNotReceive().SaveChangesAsync(...)` to prove nothing was persisted.
- Argument matchers — `Arg.Any<>`, matching on the exact returned instance.

> One NSubstitute footgun worth calling out (and deliberately avoided here): verify what was received
> **after** acting, with `Received`, rather than smuggling an `Arg.Do<>(...)` capture into the *arrange*
> step — configuring a call in arrange registers it as a received call and corrupts later `Received(1)`
> counts.

### Testcontainers + Respawn in the integration tests

The integration tests boot the **real application** in-process with
`WebApplicationFactory<Program>` and repoint only its `DbContext` at a throwaway SQL Server that
**Testcontainers** starts for the run (`new MsSqlBuilder().Build()`). Nothing else about the app is
rewired — same endpoints, same DI, same EF Core provider that production uses — which is what makes
these genuine full-stack tests rather than tests of a mock.

- **Why a real SQL Server and not the EF in-memory provider?** The in-memory provider doesn't honour
  SQL Server semantics — no unique-index enforcement, no relational transaction behaviour, `decimal`
  precision differences. A test on it can pass while production fails. The unique-SKU rule and the
  atomic order-write-plus-stock-decrement are exactly the kind of thing it would fail to catch, so the
  integration suite runs against the real provider.
- **Cost model** — starting a container and creating the schema is expensive, so it happens **once**
  for the whole assembly, via an xUnit **collection fixture** (`SqlServerContainerFixture :
  IAsyncLifetime`). What each test gets instead is the cheap thing: a clean database.
- **Respawn** provides that clean slate between tests. Rather than recreating the container or the
  database, `Respawner.ResetAsync` deletes all data while keeping the schema, and `WithReseed = true`
  restarts identity columns — so every test starts from empty with ids beginning at 1, without
  hard-coding them. The reset runs in each test's `InitializeAsync`.
- The schema is created with `EnsureCreatedAsync` (not EF migrations) to keep the sample free of
  migrations tooling while still building the real provider's schema.

## Layout

```
csharp-backend-xunit-testcontainers/
├── Backend.sln
├── Directory.Build.props        # net10.0, nullable, warnings-as-errors, latest analyzers
├── Directory.Packages.props     # central package versions (CPM)
├── global.json                  # pins the .NET SDK
├── src/OrderService.Api/        # the system under test (API + EF Core + domain/services)
└── tests/
    ├── OrderService.UnitTests.Xunit/     # xUnit + NSubstitute — order placement
    ├── OrderService.UnitTests.Nunit/     # NUnit + NSubstitute — product/SKU rules
    └── OrderService.IntegrationTests/    # xUnit + Testcontainers + Respawn + WebApplicationFactory
```

## Running the tests

Prerequisites:

- **.NET 10 SDK** (pinned in `global.json`).
- For the integration suite only: a **running Docker daemon** — Testcontainers pulls
  `mcr.microsoft.com/mssql/server` and starts a throwaway SQL Server. The unit suites need no Docker.

```bash
# everything (unit + integration)
scripts/run-tests.sh

# just the fast unit tests (xUnit + NUnit) — no Docker needed
scripts/run-tests.sh unit

# just the integration tests — Docker required
scripts/run-tests.sh integration
```

`scripts/run-tests.ps1` is the PowerShell equivalent (`-Suite all|unit|integration`). Or drive `dotnet`
directly:

```bash
dotnet test Backend.sln -c Release
```

Test results are written as `.trx` files under `artifacts/`.

## CI

- **GitHub Actions** (primary) — `.github/workflows/csharp-backend-xunit-testcontainers.yml`, path-filtered
  to this folder. It restores, checks formatting (`dotnet format --verify-no-changes`), builds, runs the
  unit tests, then runs the integration tests. `ubuntu-latest` already ships a running Docker daemon, so
  Testcontainers starts the SQL Server with no `services:` block needed. Results are uploaded as `.trx`
  artifacts and surfaced on the PR via `dorny/test-reporter`.
- **Jenkins** (secondary) — `Jenkinsfile`, mirroring the same stages. Because the integration tests need
  Docker, its header documents the agent requirements (a host with the .NET SDK *and* a Docker daemon,
  or Docker-outside-of-Docker with the socket mounted).

> **Verified in CI.** This sample targets .NET 10 and needs Docker for the integration layer, so — like
> the other C# frameworks in this repo — CI is its first real compile-and-run. If you spot a version or
> API-surface drift, the CI run is the source of truth; open an issue or PR.
