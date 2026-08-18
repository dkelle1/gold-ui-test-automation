"""Gold-layer data-quality expectations.

Gold is the business mart, so its expectations are about being complete and well-formed for consumption:
the declared schema, one row per customer, a closed set of segments, and non-negative metrics. The
cross-layer "did the aggregation conserve everything" checks live in the SQL reconciliation module; here
we assert the mart's own internal shape.
"""

from __future__ import annotations

import pytest
from pyspark.sql import DataFrame

from dataquality.checks.column_checks import accepted_values, not_null, unique, value_in_range
from dataquality.checks.suite import assert_all
from dataquality.checks.table_checks import schema_matches
from dataquality.pipeline.schemas import GOLD_CUSTOMER_VALUE, SEGMENTS

pytestmark = pytest.mark.quality


def test_gold_schema(gold_customer_value: DataFrame) -> None:
    assert_all([schema_matches(gold_customer_value, GOLD_CUSTOMER_VALUE, "gold.customer_value")])


def test_gold_keys_domains_and_measures(gold_customer_value: DataFrame) -> None:
    ds = "gold.customer_value"
    assert_all(
        [
            not_null(gold_customer_value, "customer_id", ds),
            unique(gold_customer_value, "customer_id", ds),
            not_null(gold_customer_value, "country", ds),
            accepted_values(gold_customer_value, "segment", SEGMENTS, ds),
            value_in_range(gold_customer_value, "purchase_count", ds, minimum=0),
            value_in_range(gold_customer_value, "total_revenue", ds, minimum=0),
        ]
    )


def test_gold_segment_definition_is_internally_consistent(gold_customer_value: DataFrame) -> None:
    """The segment must agree with the metrics it is derived from - a customer labelled ``no_purchase``
    must genuinely have zero purchases, and a ``high_value`` one must clear the revenue threshold. This
    guards the CASE expression in the gold SQL against drifting away from the thresholds it documents."""
    mislabelled = gold_customer_value.where(
        ((gold_customer_value.segment == "no_purchase") & (gold_customer_value.purchase_count != 0))
        | ((gold_customer_value.segment == "high_value") & (gold_customer_value.total_revenue < 500))
        | ((gold_customer_value.purchase_count == 0) & (gold_customer_value.segment != "no_purchase"))
    )
    count = mislabelled.count()
    assert count == 0, f"{count} customer(s) have a segment inconsistent with their metrics"
