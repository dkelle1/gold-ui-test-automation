"""Builders for the raw (bronze-shaped) feeds the pipeline ingests.

Everything here is deterministic given ``Settings.seed``: a run reproduces exactly, which is what lets
the tests assert precise row counts through the pipeline. The data is a mix of

  * a bulk of *clean* rows (Faker, for realistic volume and shapes), and
  * a small, fixed set of *named defect* rows injected on purpose,

so every clean/silver transformation has something real to remove or repair, and the data-quality
tests have a known-good target to assert against. The exact defect contract is documented inline and
mirrored by the layer tests under ``tests/`` and the "test strategy" section of the README.

Raw rows are all strings and all nullable (see :mod:`dataquality.pipeline.schemas`) - the faithful
shape of a CSV/JSON landing, before any typing or cleaning.
"""

from __future__ import annotations

from faker import Faker
from pyspark.sql import DataFrame, SparkSession

from dataquality.config.settings import Settings
from dataquality.pipeline.schemas import BRONZE_CUSTOMERS, BRONZE_EVENTS

# How many clean customers/events the Faker bulk produces is driven by Settings; the injected defect
# rows below are a fixed, named set independent of that volume.

# --- The fixed defect contract (kept in one place so tests and pipeline agree) -----------------------
# Customers:
#   * 1 duplicate of an existing customer_id (later signup wins on dedup)
#   * 1 row with a blank email (dropped: email is mandatory downstream)
#   * 1 row with a non-numeric customer_id (dropped: cannot be typed to the silver int key)
# Events:
#   * 1 duplicate event_id (deduplicated)
#   * 1 event referencing an unknown customer_id (dropped: orphaned reference)
#   * 1 event with an out-of-domain event_type (dropped: not a known event)
#   * 1 purchase event with a negative revenue (dropped: invalid measure)
CUSTOMER_DEFECT_DUPLICATES = 1
CUSTOMER_DEFECT_DROPPED = 2  # blank email + non-numeric id
EVENT_DEFECT_DUPLICATES = 1
EVENT_DEFECT_DROPPED = 3  # orphan + bad type + negative revenue

_COUNTRIES = ("PL", "DE", "GB", "US", "FR")


def raw_customers(spark: SparkSession, settings: Settings) -> DataFrame:
    faker = Faker()
    faker.seed_instance(settings.seed)

    rows: list[tuple[str | None, ...]] = []
    for i in range(1, settings.sample_customers + 1):
        # Raw consent arrives in mixed spellings; standardizing it to the closed domain is silver's job.
        consent = faker.random_element(("granted", "denied", "Y", "N", ""))
        rows.append(
            (
                str(i),
                faker.email(),
                faker.random_element(_COUNTRIES),
                faker.date_time_this_decade().isoformat(sep=" "),
                consent,
            )
        )

    # Named defects (see module docstring). Reference an existing id for the duplicate so dedup has a
    # real collision to resolve.
    rows.append(("1", faker.email(), "PL", "2029-12-31 23:59:59", "granted"))  # duplicate of id 1
    rows.append(
        (str(settings.sample_customers + 1), "", "DE", "2025-01-01 08:00:00", "granted")
    )  # blank email
    rows.append(("not-a-number", faker.email(), "US", "2025-02-02 09:00:00", "denied"))  # unt-ypeable id

    return spark.createDataFrame(rows, schema=BRONZE_CUSTOMERS)


def raw_events(spark: SparkSession, settings: Settings) -> DataFrame:
    faker = Faker()
    faker.seed_instance(settings.seed + 1)  # a different stream from the customers generator

    rows: list[tuple[str | None, ...]] = []
    for i in range(1, settings.sample_events + 1):
        event_type = faker.random_element(("page_view", "add_to_cart", "purchase", "email_open"))
        # Only purchases carry revenue; everything else is a zero-revenue engagement event.
        revenue = (
            f"{faker.pyfloat(min_value=5, max_value=500, right_digits=2):.2f}"
            if event_type == "purchase"
            else "0.0"
        )
        customer_id = str(faker.random_int(min=1, max=settings.sample_customers))
        rows.append(
            (
                f"evt-{i}",
                customer_id,
                event_type,
                revenue,
                faker.date_time_this_year().isoformat(sep=" "),
            )
        )

    # Named defects (see module docstring).
    rows.append(("evt-1", "1", "purchase", "42.00", "2025-06-01 10:00:00"))  # duplicate event_id evt-1
    rows.append(("evt-orphan", "999999", "page_view", "0.0", "2025-06-02 11:00:00"))  # unknown customer
    rows.append(("evt-badtype", "1", "spam", "0.0", "2025-06-03 12:00:00"))  # out-of-domain event_type
    rows.append(("evt-negrev", "1", "purchase", "-10.00", "2025-06-04 13:00:00"))  # negative revenue

    return spark.createDataFrame(rows, schema=BRONZE_EVENTS)
