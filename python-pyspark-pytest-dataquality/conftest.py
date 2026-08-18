"""Session-wide fixtures: one SparkSession and one materialized pipeline, shared by every test.

Spark is expensive to start and the medallion build is expensive to compute, so both are session-scoped
and the intermediate frames are cached - the whole suite pays for the pipeline once, then asserts
against it many times. This is deliberately *not* the test-level parallelism the UI siblings use: Spark
already parallelizes each job across the executor cores inside one JVM, so fanning the tests out across
processes (pytest-xdist) would spin up several competing Spark instances fighting for the same cores.
The correct "parallelism" knob for this stack is Spark's, not pytest's - see the README.
"""

from __future__ import annotations

import os
import platform
from collections.abc import Iterator
from pathlib import Path

import pytest
from pyspark.sql import DataFrame, SparkSession

from dataquality.config.settings import Settings, get_settings
from dataquality.pipeline.bronze import to_bronze_customers, to_bronze_events
from dataquality.pipeline.enrichment import predict_intent
from dataquality.pipeline.gold import to_gold_customer_value
from dataquality.pipeline.silver import to_silver_customers, to_silver_events
from dataquality.spark.session import build_spark_session
from dataquality.testdata.sample_data import raw_customers, raw_events


@pytest.fixture(scope="session")
def settings() -> Settings:
    return get_settings()


@pytest.fixture(scope="session")
def spark(settings: Settings) -> Iterator[SparkSession]:
    session = build_spark_session(settings)
    try:
        yield session
    finally:
        session.stop()


def _materialize(df: DataFrame) -> DataFrame:
    """Cache and force computation, so every downstream test reads the same computed frame instead of
    re-running the transformation each time it is referenced."""
    df.cache().count()
    return df


# --- The medallion pipeline, built once per session -------------------------------------------------


@pytest.fixture(scope="session")
def bronze_customers(spark: SparkSession, settings: Settings) -> DataFrame:
    return _materialize(to_bronze_customers(raw_customers(spark, settings)))


@pytest.fixture(scope="session")
def bronze_events(spark: SparkSession, settings: Settings) -> DataFrame:
    return _materialize(to_bronze_events(raw_events(spark, settings)))


@pytest.fixture(scope="session")
def silver_customers(bronze_customers: DataFrame) -> DataFrame:
    return _materialize(to_silver_customers(bronze_customers))


@pytest.fixture(scope="session")
def silver_events(bronze_events: DataFrame, silver_customers: DataFrame) -> DataFrame:
    return _materialize(to_silver_events(bronze_events, silver_customers))


@pytest.fixture(scope="session")
def gold_customer_value(
    spark: SparkSession, silver_customers: DataFrame, silver_events: DataFrame
) -> DataFrame:
    return _materialize(to_gold_customer_value(spark, silver_customers, silver_events))


@pytest.fixture(scope="session")
def enriched_customer_value(gold_customer_value: DataFrame) -> DataFrame:
    return _materialize(predict_intent(gold_customer_value))


# --- Allure environment panel -----------------------------------------------------------------------


def pytest_configure(config: pytest.Config) -> None:
    # Touch settings up front so a misconfiguration fails the run immediately, and write the Allure
    # environment file now (not at session finish) so it survives a crashed or cancelled run.
    settings = get_settings()

    results_dir = getattr(config.option, "allure_report_dir", None)
    if results_dir:
        _write_environment_file(settings, Path(results_dir))


def _write_environment_file(settings: Settings, results_dir: Path) -> None:
    results_dir.mkdir(parents=True, exist_ok=True)
    import pyspark

    is_ci = os.environ.get("CI", "").lower() == "true"
    lines = [
        f"SparkVersion={pyspark.__version__}",
        f"SparkMaster={'databricks-connect' if settings.remote_url else settings.spark_master}",
        f"ShufflePartitions={settings.shuffle_partitions}",
        f"SampleCustomers={settings.sample_customers}",
        f"SampleEvents={settings.sample_events}",
        f"Seed={settings.seed}",
        f"Python={platform.python_version()}",
        f"Java={os.environ.get('JAVA_VERSION', 'system')}",
        f"OS={platform.platform()}",
        f"CI={is_ci}",
        f"GITHUB_RUN_ID={os.environ.get('GITHUB_RUN_ID', '')}",
    ]
    (results_dir / "environment.properties").write_text("\n".join(lines) + "\n", encoding="utf-8")
