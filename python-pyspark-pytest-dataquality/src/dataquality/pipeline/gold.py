"""Gold layer: the business aggregate, expressed in Spark SQL on purpose.

Silver is written in the DataFrame API; gold is written as a SQL query. That is not inconsistency - it
mirrors how a real lakehouse team works (engineers conform in the DataFrame API, analysts and BI build
the business marts in SQL) and it keeps a genuine, reviewable piece of *advanced SQL* - CTEs, a
LEFT JOIN that must preserve every customer, conditional aggregation, and a CASE-based segmentation - in
the codebase for this suite's SQL-reconciliation tests to exercise. It is also the layer a Databricks
SQL warehouse would own, unchanged.
"""

from __future__ import annotations

from pyspark.sql import DataFrame, SparkSession

from dataquality.pipeline.schemas import GOLD_CUSTOMER_VALUE

# One row per customer with engagement + value metrics and a value segment. The LEFT JOIN is load-
# bearing: a customer with no purchases must still appear (as no_purchase / 0 / 0.0), which a plain
# INNER JOIN on the purchase aggregate would silently drop - one of the reconciliation tests exists
# specifically to catch that class of mistake.
_GOLD_CUSTOMER_VALUE_SQL = """
WITH purchases AS (
    SELECT
        customer_id,
        COUNT(*)              AS purchase_count,
        SUM(revenue)          AS total_revenue
    FROM {events}
    WHERE event_type = 'purchase'
    GROUP BY customer_id
)
SELECT
    c.customer_id,
    c.country,
    -- COUNT(*) is a bigint; the gold contract declares purchase_count as a 32-bit int (a purchase count
    -- per customer never needs more), so narrow it explicitly rather than let the type widen silently.
    CAST(COALESCE(p.purchase_count, 0) AS INT)           AS purchase_count,
    ROUND(COALESCE(p.total_revenue, 0.0), 2)             AS total_revenue,
    CASE
        WHEN COALESCE(p.purchase_count, 0) = 0     THEN 'no_purchase'
        WHEN COALESCE(p.total_revenue, 0.0) >= 500 THEN 'high_value'
        WHEN COALESCE(p.total_revenue, 0.0) >= 100 THEN 'mid_value'
        ELSE 'low_value'
    END                                                  AS segment
FROM {customers} c
LEFT JOIN purchases p ON c.customer_id = p.customer_id
"""


def to_gold_customer_value(
    spark: SparkSession, silver_customers: DataFrame, silver_events: DataFrame
) -> DataFrame:
    # Register the silver inputs as views the SQL can name. Local, run-scoped temp views (dropped when
    # the session ends) - the direct analogue of the silver Delta tables a Databricks SQL query reads.
    silver_customers.createOrReplaceTempView("silver_customers")
    silver_events.createOrReplaceTempView("silver_events")

    gold = spark.sql(_GOLD_CUSTOMER_VALUE_SQL.format(customers="silver_customers", events="silver_events"))
    return gold.select(*[f.name for f in GOLD_CUSTOMER_VALUE.fields])
