"""Bronze-layer data-quality expectations.

Bronze is the raw landing, so its contract is intentionally thin: it must match the declared bronze
schema and it must not be empty (an empty bronze means the upstream feed never arrived - the failure a
volume guard exists to catch). Notably bronze is *not* expected to be clean; the test at the bottom
asserts the known defects are still present, which is the guarantee that bronze faithfully preserves
what arrived rather than quietly filtering it. Cleaning is silver's job, tested separately.
"""

from __future__ import annotations

import pytest
from pyspark.sql import DataFrame

from dataquality.checks.suite import assert_all
from dataquality.checks.table_checks import row_count_between, schema_matches
from dataquality.pipeline.schemas import BRONZE_CUSTOMERS, BRONZE_EVENTS

pytestmark = pytest.mark.schema


def test_bronze_customers_schema_and_volume(bronze_customers: DataFrame) -> None:
    assert_all(
        [
            schema_matches(bronze_customers, BRONZE_CUSTOMERS, "bronze.customers"),
            row_count_between(bronze_customers, "bronze.customers", minimum=1),
        ]
    )


def test_bronze_events_schema_and_volume(bronze_events: DataFrame) -> None:
    assert_all(
        [
            schema_matches(bronze_events, BRONZE_EVENTS, "bronze.events"),
            row_count_between(bronze_events, "bronze.events", minimum=1),
        ]
    )


@pytest.mark.quality
def test_bronze_preserves_raw_defects(bronze_events: DataFrame) -> None:
    """Bronze must be faithful, not filtered: the injected defect rows are expected to still be here.
    If this ever passes-by-absence it means something cleaned bronze upstream of silver, which would
    hide data issues the silver tests are designed to catch."""
    defect_ids = {"evt-orphan", "evt-badtype", "evt-negrev"}
    present = {
        r["event_id"] for r in bronze_events.select("event_id").collect() if r["event_id"] in defect_ids
    }
    assert present == defect_ids, (
        f"bronze should still contain the raw defects; missing {defect_ids - present}"
    )
