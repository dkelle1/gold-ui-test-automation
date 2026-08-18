"""Transformation-level tests for the silver layer, using chispa DataFrame equality.

These build a tiny, hand-authored bronze input where every row exercises one silver rule, and assert the
*exact* resulting DataFrame. That is the complement to the data-quality tests: the DQ tests assert
properties of the output ("customer_id is unique"), while these assert the transformation's precise
behaviour ("these two rows collapse to that one, keeping the later signup"). Small and Spark-light, so
they are marked ``unit``.
"""

from __future__ import annotations

from datetime import datetime

import pytest
from chispa.dataframe_comparer import assert_df_equality
from pyspark.sql import SparkSession

from dataquality.pipeline.schemas import (
    BRONZE_CUSTOMERS,
    BRONZE_EVENTS,
    SILVER_CUSTOMERS,
    SILVER_EVENTS,
)
from dataquality.pipeline.silver import to_silver_customers, to_silver_events

pytestmark = pytest.mark.unit


def test_silver_customers_dedups_types_standardizes_and_drops(spark: SparkSession) -> None:
    bronze = spark.createDataFrame(
        [
            # id 1 twice: the later signup wins on dedup, and "Y" standardizes to "granted".
            ("1", "a@x.io", "PL", "2024-01-01 00:00:00", "Y"),
            ("1", "a@x.io", "PL", "2025-01-01 00:00:00", "granted"),
            # blank consent standardizes to the privacy-safe default "denied".
            ("2", "b@x.io", "DE", "2025-02-02 08:00:00", ""),
            # dropped: non-numeric id cannot become the integer silver key.
            ("x9", "c@x.io", "US", "2025-03-03 09:00:00", "granted"),
            # dropped: blank email is unusable downstream.
            ("3", "  ", "GB", "2025-04-04 10:00:00", "denied"),
        ],
        schema=BRONZE_CUSTOMERS,
    )

    actual = to_silver_customers(bronze)

    # Explicit silver schema so the expected key types are IntegerType, not the LongType Spark would
    # infer from bare Python ints - and so this doubles as a check that the transform emits silver types.
    expected = spark.createDataFrame(
        [
            (1, "a@x.io", "PL", datetime(2025, 1, 1, 0, 0, 0), "granted"),
            (2, "b@x.io", "DE", datetime(2025, 2, 2, 8, 0, 0), "denied"),
        ],
        schema=SILVER_CUSTOMERS,
    )

    assert_df_equality(actual, expected, ignore_row_order=True, ignore_nullable=True)


def test_silver_events_types_filters_and_enforces_referential_integrity(spark: SparkSession) -> None:
    silver_customers = spark.createDataFrame(
        [(1, "a@x.io", "PL", datetime(2025, 1, 1), "granted")],
        schema=["customer_id", "email", "country", "signup_ts", "marketing_consent"],
    )

    bronze_events = spark.createDataFrame(
        [
            ("e1", "1", "purchase", "10.00", "2025-06-01 10:00:00"),
            # dropped: out-of-domain event type.
            ("e2", "1", "spam", "0.0", "2025-06-01 11:00:00"),
            # dropped: negative revenue is an invalid measure.
            ("e3", "1", "purchase", "-5.00", "2025-06-01 12:00:00"),
            # dropped: orphaned - customer 999 is not in silver_customers.
            ("e4", "999", "page_view", "0.0", "2025-06-01 13:00:00"),
        ],
        schema=BRONZE_EVENTS,
    )

    actual = to_silver_events(bronze_events, silver_customers)

    expected = spark.createDataFrame(
        [("e1", 1, "purchase", 10.00, datetime(2025, 6, 1, 10, 0, 0))],
        schema=SILVER_EVENTS,
    )

    assert_df_equality(actual, expected, ignore_row_order=True, ignore_nullable=True)
