"""Table-level data-quality checks: shape, schema and whole-row duplication."""

from __future__ import annotations

from pyspark.sql import DataFrame
from pyspark.sql import functions as F
from pyspark.sql.types import StructType

from dataquality.checks.result import CheckResult

_SAMPLE_LIMIT = 5


def row_count_between(
    df: DataFrame, dataset: str, *, minimum: int | None = None, maximum: int | None = None
) -> CheckResult:
    """Fails if the row count falls outside [minimum, maximum]. A volume guard: catches both an empty
    load (upstream feed broke) and an implausible explosion (a join fanned out)."""
    if minimum is None and maximum is None:
        raise ValueError("row_count_between needs at least one of minimum/maximum.")

    count = df.count()
    too_few = minimum is not None and count < minimum
    too_many = maximum is not None and count > maximum
    return CheckResult(
        check="row_count_between",
        passed=not (too_few or too_many),
        dataset=dataset,
        observed=count,
        details="" if not (too_few or too_many) else f"{count} rows, expected in [{minimum}, {maximum}]",
    )


def schema_matches(
    df: DataFrame, expected: StructType, dataset: str, *, ignore_nullable: bool = True
) -> CheckResult:
    """Fails unless the dataframe's schema matches ``expected`` in column names, types and order.

    Nullability is ignored by default, on purpose: Spark's per-column ``nullable`` flag is advisory and
    almost always stays ``True`` after any non-trivial transformation (a cast or filter does not clear
    it), so comparing it strictly would make every conformed layer's schema check fail against a schema
    that documents its keys as required. Presence is enforced where it belongs - by the ``not_null``
    checks - and the declared schemas keep expressing intent. Pass ``ignore_nullable=False`` for a
    strict physical-schema comparison when that is genuinely what you mean.

    This is the data-contract check either way: it is what turns "an upstream team renamed a column or
    widened a type" from a 2am incident into a red build."""

    def fields(schema: StructType) -> dict[str, object]:
        if ignore_nullable:
            return {f.name: f.dataType.simpleString() for f in schema.fields}
        return {f.name: (f.dataType.simpleString(), f.nullable) for f in schema.fields}

    actual_fields = fields(df.schema)
    expected_fields = fields(expected)
    # Order matters for a contract, so compare the ordered name lists too, not just the field maps.
    order_matches = [f.name for f in df.schema.fields] == [f.name for f in expected.fields]
    matches = order_matches and actual_fields == expected_fields

    details = ""
    if not matches:
        diffs = []
        if not order_matches:
            diffs.append(f"column order: expected {[f.name for f in expected.fields]}, got {df.columns}")
        for name in sorted(set(actual_fields) | set(expected_fields)):
            a = actual_fields.get(name)
            e = expected_fields.get(name)
            if a != e:
                diffs.append(f"{name}: expected {e}, got {a}")
        details = "; ".join(diffs)

    return CheckResult(
        check="schema_matches",
        passed=matches,
        dataset=dataset,
        observed=0 if matches else len(df.columns),
        details=details,
    )


def no_duplicate_rows(df: DataFrame, dataset: str, subset: list[str] | None = None) -> CheckResult:
    """Fails if any row (or any combination of ``subset`` columns) appears more than once. With no
    subset it is a full-row duplication check; with a subset it is a composite-key uniqueness check."""
    grouping = subset if subset else df.columns
    dup = df.groupBy(*grouping).count().where(F.col("count") > 1)
    distinct_offending = dup.count()

    sample = [tuple(r[c] for c in grouping) for r in dup.select(*grouping).limit(_SAMPLE_LIMIT).collect()]
    label = f"no_duplicate_rows({', '.join(grouping)})" if subset else "no_duplicate_rows(*)"
    return CheckResult(
        check=label,
        passed=distinct_offending == 0,
        dataset=dataset,
        observed=distinct_offending,
        details="" if distinct_offending == 0 else f"{distinct_offending} duplicated key combination(s)",
        sample=sample,
    )
