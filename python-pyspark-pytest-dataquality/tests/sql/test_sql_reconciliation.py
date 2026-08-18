"""Cross-layer reconciliation, expressed in Spark SQL.

Single-layer checks prove each table is internally sound; reconciliation proves the numbers *tie out*
end to end - that the silver→gold aggregation neither dropped nor invented value. These are written as
SQL on registered views rather than the DataFrame API on purpose: reconciliation is exactly the kind of
thing a data analyst or a Databricks SQL alert would express in SQL, and keeping a genuine piece of
non-trivial SQL (CTEs, conditional aggregation, a FULL OUTER JOIN to find either-side discrepancies, a
window function) under test is part of the point of this framework.

Every query here is built to return the *offending* rows, so a failure's own result set is the triage
list - the assertion is simply "this discrepancy query returns nothing".
"""

from __future__ import annotations

import pytest
from pyspark.sql import DataFrame, SparkSession

from dataquality.checks.relational_checks import reconcile_measure
from dataquality.checks.suite import assert_all

pytestmark = pytest.mark.reconciliation


@pytest.fixture(autouse=True)
def _register_views(
    spark: SparkSession, silver_customers: DataFrame, silver_events: DataFrame, gold_customer_value: DataFrame
) -> None:
    silver_customers.createOrReplaceTempView("silver_customers")
    silver_events.createOrReplaceTempView("silver_events")
    gold_customer_value.createOrReplaceTempView("gold_customer_value")


def test_every_customer_appears_exactly_once_in_gold(spark: SparkSession) -> None:
    """The LEFT JOIN in the gold SQL must preserve every silver customer exactly once - no customer
    dropped (which an INNER JOIN on purchases would do to non-buyers) and none duplicated (which a
    fan-out bug would do). A FULL OUTER JOIN between the two customer sets surfaces either failure."""
    discrepancies = spark.sql(
        """
        WITH s AS (SELECT customer_id FROM silver_customers),
             g AS (SELECT customer_id, COUNT(*) AS n FROM gold_customer_value GROUP BY customer_id)
        SELECT
            s.customer_id           AS silver_customer_id,
            g.customer_id           AS gold_customer_id,
            g.n                     AS gold_row_count
        FROM s
        FULL OUTER JOIN g ON s.customer_id = g.customer_id
        WHERE g.customer_id IS NULL      -- a silver customer missing from gold
           OR s.customer_id IS NULL      -- a gold customer with no silver source
           OR g.n <> 1                   -- a customer appearing more than once in gold
        """
    )
    count = discrepancies.count()
    assert count == 0, f"{count} customer(s) do not map 1:1 from silver to gold:\n{_show(discrepancies)}"


def test_purchase_counts_reconcile_per_customer(spark: SparkSession) -> None:
    """Recompute purchase_count straight from silver events and compare it to what gold recorded, per
    customer. Any customer whose gold count disagrees with the source-of-truth silver count is returned."""
    discrepancies = spark.sql(
        """
        WITH expected AS (
            SELECT customer_id, COUNT(*) AS expected_count
            FROM silver_events
            WHERE event_type = 'purchase'
            GROUP BY customer_id
        )
        SELECT
            g.customer_id,
            g.purchase_count                       AS gold_count,
            COALESCE(e.expected_count, 0)          AS silver_count
        FROM gold_customer_value g
        LEFT JOIN expected e ON g.customer_id = e.customer_id
        WHERE g.purchase_count <> COALESCE(e.expected_count, 0)
        """
    )
    count = discrepancies.count()
    assert count == 0, (
        f"{count} customer(s) have a gold purchase_count that disagrees with silver:\n{_show(discrepancies)}"
    )


def test_total_revenue_reconciles_end_to_end(
    silver_events: DataFrame, gold_customer_value: DataFrame
) -> None:
    """The grand total of purchase revenue in silver must equal the grand total rolled into gold - the
    aggregation conserves the measure. A small tolerance absorbs floating-point summation order only.

    ``reconcile_measure`` compares the sum of one named column across two frames, so both sides are
    projected to a common ``revenue`` column first: silver's per-event ``revenue`` (purchases only) and
    gold's per-customer ``total_revenue``."""
    silver_purchase_revenue = silver_events.where(silver_events.event_type == "purchase").selectExpr(
        "revenue"
    )
    gold_revenue = gold_customer_value.selectExpr("total_revenue AS revenue")

    assert_all(
        [
            reconcile_measure(
                silver_purchase_revenue,
                gold_revenue,
                "revenue",
                "silver.events(purchase) -> gold.customer_value",
                tolerance=0.01,
            )
        ]
    )


def test_high_value_ranking_matches_window_over_revenue(spark: SparkSession) -> None:
    """An advanced-SQL spot check using a window function: the customers gold labels ``high_value`` must
    be exactly those in the top revenue band. Here we assert the label lines up with a RANK over
    total_revenue restricted to the >= 500 threshold the segment is defined by - proving the segmentation
    and an independent windowed ranking agree on the same population."""
    mismatches = spark.sql(
        """
        WITH ranked AS (
            SELECT
                customer_id,
                segment,
                total_revenue,
                RANK() OVER (ORDER BY total_revenue DESC) AS revenue_rank
            FROM gold_customer_value
        )
        SELECT customer_id, segment, total_revenue, revenue_rank
        FROM ranked
        WHERE (total_revenue >= 500 AND segment <> 'high_value')
           OR (total_revenue <  500 AND segment  = 'high_value')
        """
    )
    count = mismatches.count()
    assert count == 0, (
        f"{count} customer(s): high_value segment disagrees with the revenue band:\n{_show(mismatches)}"
    )


def _show(df: DataFrame, n: int = 10) -> str:
    rows = df.limit(n).collect()
    return "\n".join(str(r.asDict()) for r in rows)
