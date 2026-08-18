# python-pyspark-pytest-dataquality

[![CI](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/python-pyspark-pytest-dataquality.yml/badge.svg)](https://github.com/dkelle1/gold-ui-test-automation/actions/workflows/python-pyspark-pytest-dataquality.yml)

A **Big Data / data-quality** testing framework: [PySpark](https://spark.apache.org/docs/latest/api/python/)
+ [pytest](https://docs.pytest.org/) testing a Databricks-style **medallion** (bronze → silver → gold)
lakehouse pipeline. It is the gallery's data-engineering QA counterpart to the UI frameworks - the system
under test is a data pipeline, not a screen - and, because it ships its own sample dataset and needs no
external service, the whole suite runs and is verified locally.

It exists to cover the skill set of a **Big Data QA / Quality Engineering Lead** role rather than a UI
automation one:

| Skill the role asks for | Where it lives here |
|---|---|
| **Python** (hands-on) | the entire framework |
| **PySpark** | the pipeline under test *and* the test harness's data engine |
| **PyTest** | the runner, fixtures, markers, parametrization |
| **Databricks** | the medallion architecture; the same suite attaches to a cluster via Spark Connect (see [Running on Databricks](#running-the-same-suite-on-databricks)) |
| **Big Data environment** | Spark DataFrames, partitioning, executor-core parallelism, a lakehouse layering model |
| **Advanced SQL** | the gold mart and every cross-layer reconciliation are Spark SQL - CTEs, `FULL OUTER JOIN`, conditional aggregation, window functions |
| **Test strategy for complex projects** | the layered, risk-based [data-quality test strategy](#the-data-quality-test-strategy) below, backed by a reusable check library |
| *AI-powered / LLM solutions* (nice-to-have) | [metamorphic tests](#testing-the-ai-enrichment-without-a-ground-truth) for a model-style enrichment step, written to survive a non-deterministic model |
| *CDP / marketing-tech, third-party data* (nice-to-have) | the sample domain is a customer + marketing-event (clickstream) dataset, the shape a CDP produces |

## Stack

| Concern | Choice |
|---|---|
| Language | Python 3.11 |
| Data engine & SUT runtime | PySpark 4.2 (Spark 4.x - first Spark major with official Java 21 support) |
| Test runner | pytest (single-process on purpose - [see below](#a-note-on-parallelism)) |
| DataFrame assertions | [chispa](https://github.com/MrPowers/chispa) (`assert_df_equality`) |
| Fake data | Faker (raw-feed volume only; never asserted on) |
| Reporting | Allure |
| Packaging / deps | uv + `pyproject.toml` + `uv.lock` |
| CI | GitHub Actions (primary) + Jenkinsfile (secondary) |

## Prerequisites

- Python 3.11 and [uv](https://docs.astral.sh/uv/)
- A JDK 17 or 21 (PySpark needs a JVM; Spark 4.x supports both)
- Optional, for viewing reports locally: the [Allure commandline](https://allurereport.org/docs/gettingstarted-installation/)

## Quick start

```bash
cd python-pyspark-pytest-dataquality
uv sync                        # resolve + install from the lockfile into .venv
scripts/run-tests.sh           # run the whole suite  (or scripts\run-tests.ps1 on Windows)
scripts/generate-report.sh     # open the Allure report in a browser
```

## Project structure

```
python-pyspark-pytest-dataquality/
├── pyproject.toml / uv.lock         # deps + pytest/ruff config
├── Jenkinsfile                      # secondary CI
├── categories.json                  # Allure failure buckets
├── conftest.py                      # session SparkSession + the medallion built once, cached
├── scripts/                         # run-tests / generate-report (sh + ps1)
├── src/dataquality/
│   ├── config/settings.py           # Spark master, sample sizes, seed, Databricks-connect switch
│   ├── spark/session.py             # local Spark  OR  attach to a cluster over Spark Connect
│   ├── checks/                      # the reusable data-quality assertion library (the core artifact)
│   │   ├── result.py                #   CheckResult - structured, sampled, triage-friendly
│   │   ├── column_checks.py         #   not_null, unique, accepted_values, value_in_range, matches_regex
│   │   ├── table_checks.py          #   row_count_between, schema_matches, no_duplicate_rows
│   │   ├── relational_checks.py     #   referential_integrity, reconcile_measure
│   │   └── suite.py                 #   run many expectations, fail once with all failures
│   ├── pipeline/                    # the medallion pipeline UNDER TEST
│   │   ├── schemas.py               #   explicit per-layer schemas + closed domains
│   │   ├── bronze.py / silver.py / gold.py   #   raw landing → conform → business mart (gold is SQL)
│   │   └── enrichment.py            #   an AI-style intent classifier + its testable contract
│   └── testdata/sample_data.py      # deterministic raw feeds (clean bulk + a fixed defect contract)
└── tests/
    ├── unit/                        # the check library's own tests + chispa transform tests
    ├── dataquality/                 # bronze/silver/gold expectation suites
    ├── sql/                         # cross-layer Spark SQL reconciliation
    └── ai/                          # metamorphic / property tests for the enrichment
```

## The data-quality test strategy

The point of a QA *lead* framework is not a pile of assertions - it is a strategy that says *what class
of failure each layer is responsible for catching*, so coverage is legible and gaps are obvious. This
suite is organised as risk-based layers, each a pytest marker, running cheapest-and-most-fundamental
first:

| Layer (marker) | The question it answers | Example checks |
|---|---|---|
| `unit` | Do the instruments and the transforms themselves work? | the check library's own tests; chispa exact-output tests of silver transforms |
| `schema` | Did the data arrive in the shape we agreed? | `schema_matches` against each layer's declared schema; volume guards |
| `quality` | Is each row trustworthy in isolation? | not-null keys, uniqueness, closed domains, value ranges, no negative measures |
| `reconciliation` | Do the numbers tie out *between* layers? | referential integrity; per-customer count reconciliation; end-to-end revenue conservation |
| `ai` | Does the non-deterministic enrichment behave, without a ground truth? | closed output domain, idempotence, determinism, metamorphic monotonicity |

Two deliberate design decisions make this more than a checklist:

- **Checks return a structured result, they don't assert directly.** Every check yields a `CheckResult`
  carrying the offending count and a bounded sample of bad keys; an `ExpectationSuite` runs a whole list
  and fails **once with every failure**, so a broken load surfaces its full blast radius instead of one
  red test at a time. The same objects could feed a data-contract gate or a dashboard outside pytest.
- **The transformation and the check that guards it are two sides of one spec.** Every cleaning rule in
  `silver.py` has a matching expectation in `tests/dataquality/`, and every rule exists because the raw
  feed's [defect contract](src/dataquality/testdata/sample_data.py) injects a row that would violate it.
  A regression shows up as a *specific* named check going red, not a vague failure.

## The medallion pipeline under test

The system under test is a three-layer lakehouse, the same shape a Databricks project uses:

- **bronze** - the raw feed landed verbatim (all strings, all nullable), warts and all. A bronze test
  even asserts the injected defects are *still present*, proving bronze faithfully preserves rather than
  silently cleans.
- **silver** - conformed and trustworthy: typed, deduplicated, standardized categoricals, non-negative
  measures, referential integrity between events and customers. Written in the DataFrame API.
- **gold** - the business mart (per-customer value + segment), written in **Spark SQL** on purpose:
  that is the layer analysts and a Databricks SQL warehouse own, and it keeps a genuine piece of
  advanced SQL under test.

### Databricks / Delta mapping

Locally the layers are Spark DataFrames and temp views; on Databricks they are Delta tables in Unity
Catalog. The code maps over directly: the silver DataFrame transforms are notebook/job cells, the gold
SQL is a Databricks SQL query unchanged, and the temp views the reconciliation reads (`silver_customers`,
`silver_events`, `gold_customer_value`) become catalog tables. The DQ concepts (schema, integrity,
reconciliation) are identical; only the persistence layer (Parquet/DataFrame here, Delta there) differs.

## Testing the AI enrichment without a ground truth

`pipeline/enrichment.py` assigns each customer a next-best-action intent - a stand-in for an LLM/ML
classifier. There is no labelled truth to assert against, so the [`ai` tests](tests/ai/) pin behaviour
down with **metamorphic and property** relations that hold for *any* input and would still be the right
tests against a live, non-deterministic model:

- **Closed output domain** - the classifier may never emit a label outside the agreed set (the "no
  hallucinated categories" guard, the single most important property once a real LLM sits behind it).
- **Shape-preserving** - exactly one intent per row, nothing added or dropped.
- **Idempotent** and **deterministic** - re-scoring is safe and stable (for a live model, the
  "temperature 0 / fixed seed" assumption, isolated here rather than left implicit).
- **Metamorphic monotonicity** - if customer A is worth at least as much as B on every value feature,
  A's intent must rank no lower than B's. This pins down behaviour with a *relation between inputs and
  outputs* instead of a fixed expected label - the core technique for testing models without an oracle.

## A note on parallelism

The UI siblings make test-level parallelism the headline feature (a distinct browser/user per worker).
This framework deliberately runs **single-process**, and that is the *correct* choice for the stack, not
a limitation: Spark already parallelizes every job across the executor cores inside one JVM, so fanning
the tests out with `pytest-xdist` would spin up several competing Spark instances fighting for the same
cores. The parallelism knob that matters here is Spark's (`local[N]` / cluster executors), configured in
`settings.py`, not pytest's. The expensive Spark session and the medallion build are created **once** per
session (cached fixtures in `conftest.py`) and shared by every test.

## Running the same suite on Databricks

Set one environment variable and the identical tests run against a real cluster over Spark Connect -
nothing in the pipeline or the checks changes:

```bash
export DATABRICKS_CONNECT="sc://<host>:443/;token=<pat>;x-databricks-cluster-id=<cluster-id>"
scripts/run-tests.sh
```

`spark/session.py` builds a local session by default and a remote (cluster) session when
`DATABRICKS_CONNECT` is set - the switch that makes this harness a genuine Databricks CI gate rather than
a local-only toy.

## Configuration reference

Every setting has a local default and an environment-variable override (`settings.py`):

| Env var | Default | Purpose |
|---|---|---|
| `SPARK_MASTER` | `local[2]` | Local Spark master (ignored when `DATABRICKS_CONNECT` is set) |
| `DATABRICKS_CONNECT` | *(unset)* | Spark Connect endpoint; when set, the suite runs against that cluster |
| `SPARK_SHUFFLE_PARTITIONS` | `4` | Kept small - these datasets are tiny, 200 would spawn 200 empty tasks |
| `SAMPLE_CUSTOMERS` / `SAMPLE_EVENTS` | `500` / `5000` | Raw-feed volume; the DQ logic is identical at any scale |
| `DATA_SEED` | `42` | Seeds every generator, so a failing run reproduces exactly |

## Running a subset

```bash
scripts/run-tests.sh smoke                        # the fast smoke subset
scripts/run-tests.sh 'quality or reconciliation'  # a pytest marker expression
uv run pytest tests/sql -q                         # just the SQL reconciliation tests
```

## Allure report

`allure-pytest` writes results to `allure-results/`; `scripts/generate-report.sh` copies
`categories.json` in and opens the report. The Allure "Environment" panel records the Spark version,
master, sample sizes and seed for the run. In CI the workflow uploads the raw results, publishes a full
report to `gh-pages/pyspark-dataquality/`, and attaches a self-contained single-file HTML report as a
downloadable artifact.

## What was verified locally

Unlike the UI frameworks (which need a live site and a matching browser, so CI is their first real run),
this framework is fully self-contained and was run to green while authoring: **34 tests pass** against
local Spark 4.2 on Java 21 - the check-library unit tests, the chispa transform tests, every layer's
data-quality suite, the Spark SQL reconciliation, and the metamorphic AI tests - and `ruff check` +
`ruff format --check` are clean. CI re-runs the same suite on Java 21 on a clean runner.

## Known limitations / extensions

- **Parquet/DataFrame, not Delta, locally.** The Databricks mapping is documented rather than exercised
  against a real Delta table - adding `delta-spark` and asserting on Delta features (time travel, `MERGE`,
  constraints) is the natural next step and would need a Spark/Delta version pin.
- **The enrichment is a deterministic stand-in**, not a live model call. The tests are written so that
  swapping it for a real endpoint would not change them, but wiring an actual model (and its latency,
  cost and flakiness) is out of scope for a hermetic sample.
- **Great Expectations / Soda** would be the off-the-shelf alternative to the hand-rolled check library.
  The library is hand-rolled on purpose here - it shows the engineering and keeps the dependency surface
  small - but a note on when to adopt a managed DQ tool instead belongs in a real project's strategy doc.
