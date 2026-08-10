"""Silver layer: clean, type, standardize, deduplicate, and enforce referential integrity.

This is where the raw feed becomes trustworthy. Every rule here corresponds to a defect the bronze feed
can contain (see :mod:`dataquality.testdata.sample_data` for the exact defect contract), and every rule
is independently asserted by the silver data-quality tests - so the transformation and the checks that
guard it are two sides of the same specification.
"""

from __future__ import annotations

from pyspark.sql import DataFrame
from pyspark.sql import functions as F
from pyspark.sql.window import Window

from dataquality.pipeline.schemas import EVENT_TYPES, SILVER_CUSTOMERS, SILVER_EVENTS


def to_silver_customers(bronze: DataFrame) -> DataFrame:
    typed = (
        bronze
        # Drop rows whose id is not a whole number - they cannot become the integer silver key.
        .where(F.col("customer_id").rlike("^[0-9]+$"))
        # Email is mandatory downstream; a blank or NULL email means an unusable customer record.
        .where(F.col("email").isNotNull() & (F.trim(F.col("email")) != ""))
        .withColumn("customer_id", F.col("customer_id").cast("int"))
        .withColumn("signup_ts", F.to_timestamp("signup_ts"))
        .withColumn("marketing_consent", _standardize_consent(F.col("marketing_consent")))
    )

    # Deduplicate to one row per customer, keeping the most recent signup (a deterministic tie-break on
    # email keeps the result stable when two rows share a timestamp).
    latest = Window.partitionBy("customer_id").orderBy(
        F.col("signup_ts").desc_nulls_last(), F.col("email").asc()
    )
    deduped = typed.withColumn("_rn", F.row_number().over(latest)).where(F.col("_rn") == 1).drop("_rn")

    return deduped.select(*[f.name for f in SILVER_CUSTOMERS.fields])


def to_silver_events(bronze: DataFrame, silver_customers: DataFrame) -> DataFrame:
    typed = (
        bronze
        # Out-of-domain event types are not real events - drop them rather than let them pollute metrics.
        .where(F.col("event_type").isin(list(EVENT_TYPES)))
        .withColumn("customer_id", F.col("customer_id").cast("int"))
        .withColumn("revenue", F.col("revenue").cast("double"))
        .withColumn("event_ts", F.to_timestamp("event_ts"))
        # Revenue must parse and be non-negative; a negative or unparseable measure is invalid.
        .where(F.col("revenue").isNotNull() & (F.col("revenue") >= 0))
        .where(F.col("customer_id").isNotNull())
    )

    # Referential integrity: keep only events for a customer that survived into silver. An event for an
    # unknown/dropped customer is an orphan and does not belong in the conformed layer.
    known_customers = silver_customers.select(F.col("customer_id").alias("_cid")).distinct()
    referential = typed.join(known_customers, typed["customer_id"] == known_customers["_cid"], "left_semi")

    # One row per event_id, keeping the latest by event timestamp.
    latest = Window.partitionBy("event_id").orderBy(F.col("event_ts").desc_nulls_last())
    deduped = referential.withColumn("_rn", F.row_number().over(latest)).where(F.col("_rn") == 1).drop("_rn")

    return deduped.select(*[f.name for f in SILVER_EVENTS.fields])


def _standardize_consent(col):
    """Fold the raw consent spellings into the closed {granted, denied} domain. Anything unrecognized
    (including blank) is treated as *denied* - the privacy-safe default for a marketing consent flag."""
    normalized = F.lower(F.trim(col))
    return F.when(normalized.isin("granted", "y", "yes", "true", "1"), F.lit("granted")).otherwise(
        F.lit("denied")
    )
