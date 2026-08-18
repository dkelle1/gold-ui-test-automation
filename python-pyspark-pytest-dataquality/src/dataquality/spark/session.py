"""Builds the one SparkSession the whole test run shares.

The same session drives both the pipeline-under-test and the data-quality assertions, exactly as it
would on Databricks: there, ``spark`` is the ambient session the notebook/job already holds. Locally we
construct it; against Databricks we attach to the cluster's session over Spark Connect. Nothing in the
pipeline or the checks changes between the two - that portability is the whole point of writing the
harness in PySpark rather than a bespoke SQL runner.
"""

from __future__ import annotations

from pyspark.sql import SparkSession

from dataquality.config.settings import Settings, get_settings


def build_spark_session(settings: Settings | None = None) -> SparkSession:
    resolved = settings or get_settings()

    builder = SparkSession.builder.appName(resolved.app_name)

    if resolved.remote_url:
        # Spark Connect / databricks-connect path: the cluster owns master, executors and shuffle
        # config, so we only name the endpoint and let the remote session's own settings win.
        builder = builder.remote(resolved.remote_url)
    else:
        builder = (
            builder.master(resolved.spark_master)
            .config("spark.sql.shuffle.partitions", str(resolved.shuffle_partitions))
            # The UI is pure overhead for a headless test run and binds a port that collides when
            # several runs share a machine (e.g. CI matrix jobs).
            .config("spark.ui.enabled", "false")
            # Timestamps in the sample data are plain wall-clock; pinning UTC keeps assertions on them
            # independent of the machine's timezone (a classic source of "passes locally, fails in CI").
            .config("spark.sql.session.timeZone", "UTC")
        )

    spark = builder.getOrCreate()

    if not resolved.remote_url:
        # WARN and below is just noise for a test run; keep failures readable. A Spark Connect session
        # has no local sparkContext to call this on, so it is skipped on the remote path.
        spark.sparkContext.setLogLevel("ERROR")

    return spark
