"""Run configuration, resolved once from environment variables with sensible local defaults.

Unlike the UI siblings (BaseUrl, browser, headless), the knobs that matter for a Spark data-quality
suite are the Spark master, shuffle parallelism, and - the one that turns this from a toy into a
Databricks-relevant harness - whether to attach to a remote Databricks cluster via Spark Connect
instead of spinning a local Spark. See ``dataquality.spark.session`` for how ``DATABRICKS_CONNECT`` is
consumed.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache


@dataclass(frozen=True)
class Settings:
    # Local Spark master. Ignored when remote_url is set (Spark Connect owns the topology then).
    spark_master: str
    # A Spark Connect endpoint, e.g. "sc://<databricks-host>:443/;token=...;x-databricks-cluster-id=..."
    # None means build a local SparkSession. This is the single switch that makes the identical test
    # suite run against a real Databricks cluster.
    remote_url: str | None
    app_name: str
    # Kept small on purpose: these datasets are tiny, and the default 200 shuffle partitions would spawn
    # 200 near-empty tasks per aggregation - slow, and noisy in the Spark UI - for no benefit.
    shuffle_partitions: int
    # Faker volume for the raw ingest builders. Deliberately modest so the whole suite stays fast; the
    # data-quality logic is identical at any scale.
    sample_customers: int
    sample_events: int
    # Seed for every deterministic generator, so a failing run reproduces exactly.
    seed: int


def _int_env(name: str, default: int) -> int:
    raw = os.environ.get(name)
    if raw is None or raw.strip() == "":
        return default
    return int(raw)


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Loaded once and cached for the process lifetime - settings never change mid-run."""
    remote_url = os.environ.get("DATABRICKS_CONNECT") or None

    return Settings(
        spark_master=os.environ.get("SPARK_MASTER", "local[2]"),
        remote_url=remote_url,
        app_name=os.environ.get("SPARK_APP_NAME", "dataquality-tests"),
        shuffle_partitions=_int_env("SPARK_SHUFFLE_PARTITIONS", 4),
        sample_customers=_int_env("SAMPLE_CUSTOMERS", 500),
        sample_events=_int_env("SAMPLE_EVENTS", 5000),
        seed=_int_env("DATA_SEED", 42),
    )
