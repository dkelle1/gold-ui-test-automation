"""Unit tests for the data-quality check library itself.

The checks are the instrument every other test in this suite measures with, so the instrument gets its
own calibration: each check is exercised on a tiny, hand-built frame with a known-good and a known-bad
case, plus the misuse case (naming a missing column) that must raise rather than silently pass. These
are the fast, Spark-light tests - marked ``unit`` and ``smoke``.
"""

from __future__ import annotations

import pytest
from pyspark.sql import SparkSession
from pyspark.sql.types import (
    DoubleType,
    IntegerType,
    StringType,
    StructField,
    StructType,
)

from dataquality.checks.column_checks import (
    accepted_values,
    matches_regex,
    not_null,
    unique,
    value_in_range,
)
from dataquality.checks.relational_checks import reconcile_measure, referential_integrity
from dataquality.checks.table_checks import (
    no_duplicate_rows,
    row_count_between,
    schema_matches,
)

pytestmark = [pytest.mark.unit, pytest.mark.smoke]

_SCHEMA = StructType(
    [
        StructField("id", IntegerType(), True),
        StructField("category", StringType(), True),
        StructField("amount", DoubleType(), True),
    ]
)


def _df(spark: SparkSession, rows: list[tuple]):
    return spark.createDataFrame(rows, schema=_SCHEMA)


def test_not_null_passes_and_fails(spark: SparkSession) -> None:
    ok = _df(spark, [(1, "a", 1.0), (2, "b", 2.0)])
    assert not_null(ok, "id", "t").passed

    bad = _df(spark, [(1, "a", 1.0), (None, "b", 2.0)])
    result = not_null(bad, "id", "t")
    assert not result.passed
    assert result.observed == 1


def test_unique_ignores_nulls_but_catches_duplicates(spark: SparkSession) -> None:
    ok = _df(spark, [(1, "a", 1.0), (2, "b", 2.0), (None, "c", 3.0)])
    assert unique(ok, "id", "t").passed, "two NULLs are not a duplicate"

    bad = _df(spark, [(1, "a", 1.0), (1, "b", 2.0)])
    result = unique(bad, "id", "t")
    assert not result.passed
    assert result.sample == [1]


def test_accepted_values_treats_null_and_unknown_as_out_of_domain(spark: SparkSession) -> None:
    ok = _df(spark, [(1, "a", 1.0), (2, "b", 2.0)])
    assert accepted_values(ok, "category", ["a", "b"], "t").passed

    bad = _df(spark, [(1, "a", 1.0), (2, "z", 2.0), (3, None, 3.0)])
    result = accepted_values(bad, "category", ["a", "b"], "t")
    assert not result.passed
    assert result.observed == 2  # "z" and NULL


def test_value_in_range_one_and_two_sided(spark: SparkSession) -> None:
    ok = _df(spark, [(1, "a", 0.0), (2, "b", 50.0)])
    assert value_in_range(ok, "amount", "t", minimum=0).passed

    bad = _df(spark, [(1, "a", -1.0), (2, "b", 5.0)])
    assert not value_in_range(bad, "amount", "t", minimum=0).passed

    bounded = _df(spark, [(1, "a", 5.0), (2, "b", 150.0)])
    result = value_in_range(bounded, "amount", "t", minimum=0, maximum=100)
    assert not result.passed
    assert result.observed == 1


def test_matches_regex_is_anchored_and_skips_nulls(spark: SparkSession) -> None:
    ok = _df(spark, [(1, "AB12", 1.0), (2, "CD34", 2.0), (3, None, 3.0)])
    assert matches_regex(ok, "category", "[A-Z]{2}[0-9]{2}", "t").passed, "NULL skipped, rest match"

    bad = _df(spark, [(1, "AB12", 1.0), (2, "AB123", 2.0)])
    # Anchored: "AB123" must fail even though "AB12" is a prefix of it.
    assert not matches_regex(bad, "category", "[A-Z]{2}[0-9]{2}", "t").passed


def test_row_count_between(spark: SparkSession) -> None:
    df = _df(spark, [(1, "a", 1.0), (2, "b", 2.0)])
    assert row_count_between(df, "t", minimum=1, maximum=10).passed
    assert not row_count_between(df, "t", minimum=5).passed


def test_schema_matches_reports_the_diff(spark: SparkSession) -> None:
    df = _df(spark, [(1, "a", 1.0)])
    assert schema_matches(df, _SCHEMA, "t").passed

    # A type change is caught regardless of the nullability mode.
    widened = StructType(_SCHEMA.fields[:-1] + [StructField("amount", StringType(), True)])
    result = schema_matches(df, widened, "t")
    assert not result.passed
    assert "amount" in result.details


def test_schema_matches_ignores_nullability_by_default_but_can_be_strict(spark: SparkSession) -> None:
    df = _df(spark, [(1, "a", 1.0)])  # inferred schema: all nullable=True
    all_required = StructType([StructField(f.name, f.dataType, False) for f in _SCHEMA.fields])

    # Default: nullability differences are tolerated (names + types match).
    assert schema_matches(df, all_required, "t").passed
    # Strict: the nullable=True vs required=False difference is now a failure.
    assert not schema_matches(df, all_required, "t", ignore_nullable=False).passed


def test_no_duplicate_rows_full_and_subset(spark: SparkSession) -> None:
    unique_rows = _df(spark, [(1, "a", 1.0), (2, "a", 2.0)])
    assert no_duplicate_rows(unique_rows, "t").passed
    # Same category twice -> a subset (composite-key) duplicate even though full rows differ.
    result = no_duplicate_rows(unique_rows, "t", subset=["category"])
    assert not result.passed


def test_referential_integrity_flags_orphans(spark: SparkSession) -> None:
    child = _df(spark, [(1, "a", 1.0), (2, "b", 2.0)])
    parent = spark.createDataFrame([(1,), (2,)], schema=StructType([StructField("pid", IntegerType())]))
    assert referential_integrity(child, "id", parent, "pid", "t").passed

    orphan_child = _df(spark, [(1, "a", 1.0), (99, "b", 2.0)])
    result = referential_integrity(orphan_child, "id", parent, "pid", "t")
    assert not result.passed
    assert result.sample == [99]


def test_reconcile_measure_ties_out(spark: SparkSession) -> None:
    left = _df(spark, [(1, "a", 10.0), (2, "b", 20.0)])
    right = _df(spark, [(1, "a", 30.0)])  # same total, different shape
    assert reconcile_measure(left, right, "amount", "t").passed

    mismatched = _df(spark, [(1, "a", 5.0)])
    assert not reconcile_measure(left, mismatched, "amount", "t").passed


def test_missing_column_raises_rather_than_failing_softly(spark: SparkSession) -> None:
    df = _df(spark, [(1, "a", 1.0)])
    # A test that names a column that does not exist is a test bug, not a data defect - it must raise,
    # loudly and differently from a normal check failure.
    with pytest.raises(ValueError, match="not present"):
        not_null(df, "does_not_exist", "t")
