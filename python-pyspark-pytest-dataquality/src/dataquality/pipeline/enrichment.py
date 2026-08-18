"""An AI-style enrichment step, and the contract that makes it testable without a ground-truth label.

``predict_intent`` stands in for an LLM / ML classifier that assigns each customer a next-best-action.
Here it is a deterministic scoring function so the suite is fast and hermetic, but it is deliberately
treated by the tests as a black box: the AI tests assert *metamorphic and property* invariants
(idempotence, closed output domain, a monotonicity relation between input value and output intent),
never an exact predicted label for an arbitrary row. That is exactly the technique you need when the
real thing is a non-deterministic model with no oracle - swapping this stand-in for a live model
endpoint would not change a single one of those tests.

The contract the tests hold the classifier to:

* **Closed domain.** Every output is one of :data:`INTENTS` - the model may never emit a label the
  downstream systems don't understand (the "no hallucinated categories" guard).
* **Total & shape-preserving.** Exactly one intent per input row; no row added or dropped; only the new
  column appears.
* **Deterministic** for a given input (a property a live model would relax to "stable under a fixed
  seed/temperature=0", which is why the test isolates it rather than assuming it everywhere).
* **Monotonic in value.** If customer A is worth at least as much as B on every value feature
  (purchase_count and total_revenue), A's intent must rank no lower than B's by :data:`INTENT_PRIORITY`.
  This is the metamorphic relation that pins down behaviour without labelled data.
"""

from __future__ import annotations

from pyspark.sql import Column, DataFrame
from pyspark.sql import functions as F

# The closed set of actions the classifier may return, and their value ranking (higher = more valuable
# customer intent). The monotonicity property is defined against this ordering.
INTENTS = ("nurture", "reactivate", "retain", "upsell")
INTENT_PRIORITY = {label: rank for rank, label in enumerate(INTENTS)}


def _intent_expr() -> Column:
    # A pure function of the two value features, thresholded so it is monotonic in both: more revenue or
    # more purchases can only move a customer to an equal-or-higher-priority intent, never a lower one.
    return (
        F.when(F.col("total_revenue") >= 500, F.lit("upsell"))
        .when(F.col("total_revenue") >= 100, F.lit("retain"))
        .when(F.col("purchase_count") >= 1, F.lit("reactivate"))
        .otherwise(F.lit("nurture"))
    )


def predict_intent(gold_customer_value: DataFrame) -> DataFrame:
    """Adds a ``predicted_intent`` column to the gold customer-value table. Idempotent: applying it to a
    frame that already has the column replaces it with the same value."""
    return gold_customer_value.withColumn("predicted_intent", _intent_expr())
