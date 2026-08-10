"""Explicit schemas for every layer.

Schemas are declared, never inferred. Inference is convenient in a notebook and a liability in a
pipeline: it makes the contract depend on whatever happened to be in today's sample, so a column that
is all-NULL today infers as the wrong type tomorrow. Declaring them makes each schema a reviewable
artifact and lets :func:`dataquality.checks.table_checks.schema_matches` assert against it.

The bronze schema is deliberately all-strings-and-nullable: bronze is the faithful landing of the raw
feed, warts and all (bad types, blanks, dupes). Cleaning and typing is silver's job, and the schema
tightening from bronze to silver is itself part of what the tests assert.
"""

from __future__ import annotations

from pyspark.sql.types import (
    DoubleType,
    IntegerType,
    StringType,
    StructField,
    StructType,
    TimestampType,
)

# Raw customer feed: everything lands as a nullable string, exactly as a CSV/JSON drop would.
BRONZE_CUSTOMERS = StructType(
    [
        StructField("customer_id", StringType(), True),
        StructField("email", StringType(), True),
        StructField("country", StringType(), True),
        StructField("signup_ts", StringType(), True),
        StructField("marketing_consent", StringType(), True),
    ]
)

# Raw marketing-event feed (a CDP-style clickstream): also all strings at landing.
BRONZE_EVENTS = StructType(
    [
        StructField("event_id", StringType(), True),
        StructField("customer_id", StringType(), True),
        StructField("event_type", StringType(), True),
        StructField("revenue", StringType(), True),
        StructField("event_ts", StringType(), True),
    ]
)

# Conformed customers: typed, deduplicated, one row per customer, mandatory fields non-nullable.
SILVER_CUSTOMERS = StructType(
    [
        StructField("customer_id", IntegerType(), False),
        StructField("email", StringType(), False),
        StructField("country", StringType(), False),
        StructField("signup_ts", TimestampType(), False),
        StructField("marketing_consent", StringType(), False),
    ]
)

# Conformed events: typed, only rows that reference a known customer and a known event type.
SILVER_EVENTS = StructType(
    [
        StructField("event_id", StringType(), False),
        StructField("customer_id", IntegerType(), False),
        StructField("event_type", StringType(), False),
        StructField("revenue", DoubleType(), False),
        StructField("event_ts", TimestampType(), False),
    ]
)

# Business aggregate: one row per customer with their engagement + value metrics.
GOLD_CUSTOMER_VALUE = StructType(
    [
        StructField("customer_id", IntegerType(), False),
        StructField("country", StringType(), False),
        StructField("purchase_count", IntegerType(), False),
        StructField("total_revenue", DoubleType(), False),
        StructField("segment", StringType(), False),
    ]
)

# The closed domains the pipeline conforms to. Referenced by both the pipeline and the DQ tests, so the
# two can never disagree about what "valid" means.
EVENT_TYPES = ("page_view", "add_to_cart", "purchase", "email_open")
CONSENT_VALUES = ("granted", "denied")
SEGMENTS = ("high_value", "mid_value", "low_value", "no_purchase")
