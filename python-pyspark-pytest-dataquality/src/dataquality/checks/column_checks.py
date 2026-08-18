"""Row-level, single-column data-quality checks.

Each returns a :class:`CheckResult`; none raises on a data problem (only on misuse, e.g. naming a
column that does not exist - that is a test bug, not a data defect, and should fail loudly and
differently). Every check computes its verdict in one Spark pass and pulls back at most a small sample
of offending keys, never the full failing set.
"""

from __future__ import annotations

from collections.abc import Iterable
from typing import Any

from pyspark.sql import Column, DataFrame
from pyspark.sql import functions as F

from dataquality.checks.result import CheckResult

_SAMPLE_LIMIT = 5


def _require_columns(df: DataFrame, *columns: str) -> None:
    missing = [c for c in columns if c not in df.columns]
    if missing:
        raise ValueError(
            f"Check references column(s) {missing} not present in dataframe columns {df.columns}. "
            "This is a test-definition error, not a data-quality failure."
        )


def _sample_keys(df: DataFrame, predicate: Column, key: str) -> list[Any]:
    rows = df.where(predicate).select(key).limit(_SAMPLE_LIMIT).collect()
    return [r[key] for r in rows]


def not_null(df: DataFrame, column: str, dataset: str, key: str | None = None) -> CheckResult:
    """Fails if any value in ``column`` is NULL. ``key`` names the column reported in the sample of
    offending rows (defaults to the checked column itself)."""
    _require_columns(df, column)
    key = key or column
    _require_columns(df, key)

    bad = df.where(F.col(column).isNull())
    count = bad.count()
    return CheckResult(
        check=f"not_null({column})",
        passed=count == 0,
        dataset=dataset,
        observed=count,
        details="" if count == 0 else f"{count} NULL value(s)",
        sample=_sample_keys(df, F.col(column).isNull(), key) if count else [],
    )


def unique(df: DataFrame, column: str, dataset: str) -> CheckResult:
    """Fails if ``column`` has any duplicated non-NULL value. NULLs are not compared for uniqueness
    (two NULLs are not "equal") - use :func:`not_null` alongside this when a key must be both."""
    _require_columns(df, column)

    dup = df.where(F.col(column).isNotNull()).groupBy(column).count().where(F.col("count") > 1)
    dup_values = [r[column] for r in dup.select(column).limit(_SAMPLE_LIMIT).collect()]
    distinct_offending = dup.count()
    return CheckResult(
        check=f"unique({column})",
        passed=distinct_offending == 0,
        dataset=dataset,
        observed=distinct_offending,
        details="" if distinct_offending == 0 else f"{distinct_offending} value(s) appear more than once",
        sample=dup_values,
    )


def accepted_values(df: DataFrame, column: str, allowed: Iterable[Any], dataset: str) -> CheckResult:
    """Fails if ``column`` holds any value outside ``allowed`` (a closed domain / enum). NULLs are
    treated as out-of-domain - a categorical column that may be NULL should be filtered first or the
    domain should include None explicitly."""
    _require_columns(df, column)
    allowed_list = list(allowed)

    predicate = ~F.col(column).isin(allowed_list) | F.col(column).isNull()
    bad = df.where(predicate)
    count = bad.count()
    offending = [r[column] for r in bad.select(column).distinct().limit(_SAMPLE_LIMIT).collect()]
    return CheckResult(
        check=f"accepted_values({column})",
        passed=count == 0,
        dataset=dataset,
        observed=count,
        details="" if count == 0 else f"{count} row(s) outside domain {allowed_list}",
        sample=offending,
    )


def value_in_range(
    df: DataFrame,
    column: str,
    dataset: str,
    *,
    minimum: float | None = None,
    maximum: float | None = None,
    inclusive: bool = True,
) -> CheckResult:
    """Fails if any non-NULL value falls outside [minimum, maximum]. Either bound may be omitted for a
    one-sided constraint (e.g. amount >= 0). NULLs pass here - pair with :func:`not_null` if required."""
    _require_columns(df, column)
    if minimum is None and maximum is None:
        raise ValueError("value_in_range needs at least one of minimum/maximum.")

    col = F.col(column)
    conditions = []
    if minimum is not None:
        conditions.append(col < minimum if inclusive else col <= minimum)
    if maximum is not None:
        conditions.append(col > maximum if inclusive else col >= maximum)

    out_of_range = conditions[0]
    for extra in conditions[1:]:
        out_of_range = out_of_range | extra
    predicate = col.isNotNull() & out_of_range

    bad = df.where(predicate)
    count = bad.count()
    bounds = f"[{minimum}, {maximum}]" if inclusive else f"({minimum}, {maximum})"
    return CheckResult(
        check=f"value_in_range({column})",
        passed=count == 0,
        dataset=dataset,
        observed=count,
        details="" if count == 0 else f"{count} value(s) outside {bounds}",
        sample=[r[column] for r in bad.select(column).limit(_SAMPLE_LIMIT).collect()],
    )


def matches_regex(df: DataFrame, column: str, pattern: str, dataset: str) -> CheckResult:
    """Fails if any non-NULL value does not fully match ``pattern`` (anchored - the whole string must
    match, matching most format-validation intent). NULLs pass; pair with :func:`not_null` if needed."""
    _require_columns(df, column)

    col = F.col(column)
    predicate = col.isNotNull() & ~col.rlike(f"^(?:{pattern})$")
    bad = df.where(predicate)
    count = bad.count()
    return CheckResult(
        check=f"matches_regex({column})",
        passed=count == 0,
        dataset=dataset,
        observed=count,
        details="" if count == 0 else f"{count} value(s) do not match /{pattern}/",
        sample=[r[column] for r in bad.select(column).limit(_SAMPLE_LIMIT).collect()],
    )
