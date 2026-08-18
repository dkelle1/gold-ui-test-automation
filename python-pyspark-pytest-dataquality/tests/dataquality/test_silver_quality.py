"""Silver-layer data-quality expectations - the core of the suite.

Silver is the "trustworthy" layer, so it carries the full weight of expectations: a tight schema, keys
that are present and unique, categoricals inside their closed domains, measures in range, and
referential integrity between events and customers. Each expectation here corresponds to exactly one
rule in ``dataquality.pipeline.silver`` and one defect in the raw feed's defect contract - so a silver
transformation regression shows up as a specific red check naming the offending rows, not a vague
failure.
"""

from __future__ import annotations

import pytest
from pyspark.sql import DataFrame

from dataquality.checks.column_checks import (
    accepted_values,
    matches_regex,
    not_null,
    unique,
    value_in_range,
)
from dataquality.checks.relational_checks import referential_integrity
from dataquality.checks.suite import assert_all
from dataquality.checks.table_checks import no_duplicate_rows, schema_matches
from dataquality.pipeline.schemas import (
    CONSENT_VALUES,
    EVENT_TYPES,
    SILVER_CUSTOMERS,
    SILVER_EVENTS,
)

pytestmark = pytest.mark.quality


def test_silver_customers_schema(silver_customers: DataFrame) -> None:
    assert_all([schema_matches(silver_customers, SILVER_CUSTOMERS, "silver.customers")])


def test_silver_customers_keys_and_domains(silver_customers: DataFrame) -> None:
    ds = "silver.customers"
    assert_all(
        [
            not_null(silver_customers, "customer_id", ds),
            unique(silver_customers, "customer_id", ds),
            not_null(silver_customers, "email", ds),
            # A pragmatic email shape check - presence of a local part, an @, and a dotted domain. Not
            # RFC 5322 (deliberately: over-strict email regexes reject valid addresses), just enough to
            # catch a blatantly malformed value surviving into the conformed layer.
            matches_regex(silver_customers, "email", r"[^@\s]+@[^@\s]+\.[^@\s]+", ds),
            not_null(silver_customers, "marketing_consent", ds),
            accepted_values(silver_customers, "marketing_consent", CONSENT_VALUES, ds),
            not_null(silver_customers, "signup_ts", ds),
        ]
    )


def test_silver_events_schema(silver_events: DataFrame) -> None:
    assert_all([schema_matches(silver_events, SILVER_EVENTS, "silver.events")])


def test_silver_events_keys_domains_and_measures(silver_events: DataFrame) -> None:
    ds = "silver.events"
    assert_all(
        [
            not_null(silver_events, "event_id", ds),
            no_duplicate_rows(silver_events, ds, subset=["event_id"]),
            not_null(silver_events, "customer_id", ds),
            accepted_values(silver_events, "event_type", EVENT_TYPES, ds),
            not_null(silver_events, "revenue", ds),
            # The invalid-measure guard: no negative revenue survives conforming.
            value_in_range(silver_events, "revenue", ds, minimum=0),
            not_null(silver_events, "event_ts", ds),
        ]
    )


@pytest.mark.reconciliation
def test_silver_events_reference_known_customers(
    silver_events: DataFrame, silver_customers: DataFrame
) -> None:
    """Referential integrity: every silver event points at a customer that exists in silver. This is
    the check the orphaned-event defect (``evt-orphan``) is designed to trip if the silver join is ever
    weakened."""
    assert_all(
        [
            referential_integrity(
                silver_events, "customer_id", silver_customers, "customer_id", "silver.events"
            )
        ]
    )
