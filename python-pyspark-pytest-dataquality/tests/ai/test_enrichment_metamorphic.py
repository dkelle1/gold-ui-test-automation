"""Metamorphic and property tests for the AI-style enrichment step.

The enrichment stands in for a model that assigns each customer a next-best-action. There is no
ground-truth label to assert against - the point of these tests is that you do not need one. Instead
they pin down behaviour with relations that must hold for *any* input: a closed output domain, shape
preservation, idempotence, determinism, and a metamorphic monotonicity relation between input value and
output intent. Every one of these would still be the right test if ``predict_intent`` were a live,
non-deterministic model call rather than the deterministic stand-in used here.
"""

from __future__ import annotations

import pytest
from pyspark.sql import DataFrame, SparkSession
from pyspark.sql import functions as F

from dataquality.checks.column_checks import accepted_values, not_null
from dataquality.checks.suite import assert_all
from dataquality.pipeline.enrichment import INTENT_PRIORITY, INTENTS, predict_intent
from dataquality.pipeline.schemas import GOLD_CUSTOMER_VALUE

pytestmark = pytest.mark.ai


def test_output_domain_is_closed(enriched_customer_value: DataFrame) -> None:
    """The model may never emit a label outside the agreed set - the "no hallucinated categories"
    guard, and the single most important property when a real LLM sits behind this step."""
    ds = "gold.customer_value.enriched"
    assert_all(
        [
            not_null(enriched_customer_value, "predicted_intent", ds),
            accepted_values(enriched_customer_value, "predicted_intent", INTENTS, ds),
        ]
    )


def test_enrichment_preserves_shape(
    gold_customer_value: DataFrame, enriched_customer_value: DataFrame
) -> None:
    """Exactly one intent per input row: no rows added or dropped, and only the new column appears."""
    assert enriched_customer_value.count() == gold_customer_value.count()
    assert set(enriched_customer_value.columns) == set(gold_customer_value.columns) | {"predicted_intent"}


def test_enrichment_is_idempotent(enriched_customer_value: DataFrame) -> None:
    """Applying the enrichment to an already-enriched frame replaces the column with the same value -
    re-scoring is safe. A relation that protects against a real pipeline re-running the step on a
    backfill. Compared via a join on the customer key so the two runs are aligned row-for-row rather
    than by (unguaranteed) DataFrame ordering."""
    once = enriched_customer_value.select("customer_id", F.col("predicted_intent").alias("first"))
    twice = predict_intent(enriched_customer_value).select(
        "customer_id", F.col("predicted_intent").alias("second")
    )
    joined = once.join(twice, "customer_id")
    disagreements = joined.where(joined["first"] != joined["second"]).count()
    assert disagreements == 0


def test_enrichment_is_deterministic(gold_customer_value: DataFrame) -> None:
    """Two independent applications to the same input agree on every row. For a live model this is the
    "stable at temperature 0 / fixed seed" property; isolating it here documents the assumption the rest
    of the suite leans on rather than letting it be implicit."""
    a = predict_intent(gold_customer_value).select("customer_id", F.col("predicted_intent").alias("a"))
    b = predict_intent(gold_customer_value).select("customer_id", F.col("predicted_intent").alias("b"))
    joined = a.join(b, "customer_id")
    disagreements = joined.where(joined["a"] != joined["b"]).count()
    assert disagreements == 0


def test_intent_is_monotonic_in_value(spark: SparkSession) -> None:
    """The metamorphic relation: if customer A dominates B on every value feature (>= purchase_count and
    >= total_revenue), A's intent must rank no lower than B's. This is asserted on a purpose-built lattice
    of inputs so the domination pairs are known, independent of the sample data."""
    rows = [
        (1, "PL", 0, 0.0, "seg"),
        (2, "PL", 1, 50.0, "seg"),
        (3, "PL", 2, 150.0, "seg"),
        (4, "PL", 5, 600.0, "seg"),
    ]
    frame = spark.createDataFrame(
        rows, schema=["customer_id", "country", "purchase_count", "total_revenue", "segment"]
    )
    # Reuse the real gold schema's column set so predict_intent sees the shape it expects.
    frame = frame.select(*[f.name for f in GOLD_CUSTOMER_VALUE.fields])

    scored = {
        r["customer_id"]: r["predicted_intent"]
        for r in predict_intent(frame).select("customer_id", "predicted_intent").collect()
    }

    # Every ordered pair (a, b) where a dominates b must satisfy priority(a) >= priority(b).
    features = {r[0]: (r[2], r[3]) for r in rows}
    for a in features:
        for b in features:
            a_pc, a_rev = features[a]
            b_pc, b_rev = features[b]
            if a_pc >= b_pc and a_rev >= b_rev:
                assert INTENT_PRIORITY[scored[a]] >= INTENT_PRIORITY[scored[b]], (
                    f"customer {a} dominates {b} on value but got a lower-priority intent "
                    f"({scored[a]} < {scored[b]})"
                )
