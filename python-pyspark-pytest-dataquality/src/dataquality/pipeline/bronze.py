"""Bronze layer: the faithful landing of the raw feed.

Bronze does not clean or type anything - that is silver's job. It exists as its own layer so the raw
feed is captured verbatim (auditable, replayable) before any transformation could hide what actually
arrived. Here that means conforming the incoming columns to the canonical bronze schema/order and
nothing else, which is why these functions look almost like identities: that is the correct amount of
work for a bronze step.
"""

from __future__ import annotations

from pyspark.sql import DataFrame

from dataquality.pipeline.schemas import BRONZE_CUSTOMERS, BRONZE_EVENTS


def to_bronze_customers(raw: DataFrame) -> DataFrame:
    return raw.select(*[f.name for f in BRONZE_CUSTOMERS.fields])


def to_bronze_events(raw: DataFrame) -> DataFrame:
    return raw.select(*[f.name for f in BRONZE_EVENTS.fields])
