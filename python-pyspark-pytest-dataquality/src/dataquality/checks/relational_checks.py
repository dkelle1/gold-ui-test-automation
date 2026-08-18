"""Cross-table / cross-layer checks: referential integrity and reconciliation.

These are the checks that catch the failures a single-table check cannot: an orphaned foreign key, or a
silver→gold aggregation that silently dropped or double-counted rows. In a medallion lakehouse they are
the difference between "each table looks fine in isolation" and "the numbers actually tie out end to
end", which is the property a business stakeholder ultimately cares about.
"""

from __future__ import annotations

from pyspark.sql import DataFrame
from pyspark.sql import functions as F

from dataquality.checks.result import CheckResult

_SAMPLE_LIMIT = 5


def referential_integrity(
    child: DataFrame,
    child_key: str,
    parent: DataFrame,
    parent_key: str,
    dataset: str,
) -> CheckResult:
    """Fails if any non-NULL ``child.child_key`` has no matching ``parent.parent_key`` - an orphaned
    foreign key. NULL child keys are ignored (an absent reference is not a broken one; enforce presence
    separately with not_null if the relationship is mandatory)."""
    if child_key not in child.columns:
        raise ValueError(f"child_key '{child_key}' not in {child.columns}")
    if parent_key not in parent.columns:
        raise ValueError(f"parent_key '{parent_key}' not in {parent.columns}")

    parent_keys = parent.select(F.col(parent_key).alias("_pk")).distinct()
    orphans = child.where(F.col(child_key).isNotNull()).join(
        parent_keys, child[child_key] == parent_keys["_pk"], "left_anti"
    )
    count = orphans.count()
    sample = [r[child_key] for r in orphans.select(child_key).distinct().limit(_SAMPLE_LIMIT).collect()]
    return CheckResult(
        check=f"referential_integrity({child_key} -> {parent_key})",
        passed=count == 0,
        dataset=dataset,
        observed=count,
        details="" if count == 0 else f"{count} orphaned row(s) with no matching parent key",
        sample=sample,
    )


def reconcile_measure(
    left: DataFrame,
    right: DataFrame,
    measure_column: str,
    dataset: str,
    *,
    tolerance: float = 0.0,
) -> CheckResult:
    """Fails if the total of ``measure_column`` differs between two datasets by more than ``tolerance``.

    The canonical use is proving an aggregation conserves a quantity: the sum of order amounts in silver
    must equal the sum of the same amounts rolled into gold. ``tolerance`` allows for float rounding
    (leave it 0 for exact integer/decimal reconciliation)."""
    left_total = left.agg(F.sum(measure_column).alias("t")).collect()[0]["t"] or 0
    right_total = right.agg(F.sum(measure_column).alias("t")).collect()[0]["t"] or 0
    diff = abs(float(left_total) - float(right_total))
    passed = diff <= tolerance
    return CheckResult(
        check=f"reconcile_measure(sum {measure_column})",
        passed=passed,
        dataset=dataset,
        observed=int(diff) if diff.is_integer() else 0,
        details=""
        if passed
        else f"left sum={left_total}, right sum={right_total}, diff={diff} > {tolerance}",
    )
