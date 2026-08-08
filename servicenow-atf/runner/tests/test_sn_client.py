"""sn_client against a fully mocked CI/CD API - every shape the runner must survive.

The mock payloads mirror the documented sn_cicd responses (progress status codes as strings,
rolled-up counts as strings, links objects), so these tests double as executable documentation
of the API contract the client assumes.
"""

from __future__ import annotations

import pytest
import responses

from atf_runner.sn_client import AtfRunError, Counts, ServiceNowClient

BASE = "https://dev1.example.service-now.com"
RUN_URL = f"{BASE}/api/sn_cicd/testsuite/run"
PROGRESS_URL = f"{BASE}/api/sn_cicd/progress/p1"
RESULTS_URL = f"{BASE}/api/sn_cicd/testsuite/results/r1"
TABLE_URL = f"{BASE}/api/now/table/sys_atf_test_result"
UI_RESULT_URL = f"{BASE}/sys_atf_test_suite_result.do?sys_id=sr1"


def _client() -> ServiceNowClient:
    return ServiceNowClient(f"{BASE}/", "atf.ci", "secret")


def _run_to_results(client: ServiceNowClient) -> str:
    return client.wait_for_completion(
        client.start_suite(suite_name="[CSM] Smoke"), poll_interval=0, timeout=5
    )


def _register_start() -> None:
    responses.add(
        responses.POST,
        RUN_URL,
        json={"result": {"status": "0", "links": {"progress": {"id": "p1", "url": PROGRESS_URL}}}},
    )


def _register_results(*, failures: int = 0, success: int = 3) -> None:
    responses.add(
        responses.GET,
        RESULTS_URL,
        json={
            "result": {
                "test_suite_status": "failure" if failures else "success",
                "test_suite_duration": "00:01:02",
                "rolled_up_test_success_count": str(success),
                "rolled_up_test_failure_count": str(failures),
                "rolled_up_test_error_count": "0",
                "rolled_up_test_skip_count": "0",
                "links": {"results": {"id": "sr1", "url": UI_RESULT_URL}},
            }
        },
    )


@responses.activate
def test_full_run_with_per_test_detail() -> None:
    _register_start()
    responses.add(responses.GET, PROGRESS_URL, json={"result": {"status": "1", "percent_complete": "40"}})
    responses.add(
        responses.GET,
        PROGRESS_URL,
        json={"result": {"status": "2", "links": {"results": {"id": "r1", "url": RESULTS_URL}}}},
    )
    _register_results()
    responses.add(
        responses.GET,
        TABLE_URL,
        json={
            "result": [
                {
                    "test": {"value": "t1", "display_value": "[CSM][Smoke] Agent creates a case"},
                    "status": {"value": "success", "display_value": "Passed"},
                    "duration": {"value": "1970-01-01 00:00:24", "display_value": "24 Seconds"},
                    "output": {"value": "", "display_value": ""},
                },
                {
                    "test": {"value": "t2", "display_value": "[CSM][Smoke] Server-created case defaults"},
                    "status": {"value": "success", "display_value": "Passed"},
                    # Older duration layout - _parse_duration must take both.
                    "duration": {"value": "00:00:07", "display_value": "7 Seconds"},
                    "output": {"value": "", "display_value": ""},
                },
                {
                    "test": {"value": "t3", "display_value": "[CSM][Smoke] Customer data separation"},
                    "status": {"value": "success", "display_value": "Passed"},
                    "duration": {"value": "", "display_value": ""},
                    "output": {"value": "", "display_value": ""},
                },
            ]
        },
    )

    client = _client()
    seen: list[str] = []
    progress_url = client.start_suite(suite_name="[CSM] Smoke")
    results_url = client.wait_for_completion(
        progress_url, poll_interval=0, timeout=5, on_progress=lambda label, _pct: seen.append(label)
    )
    outcome = client.fetch_suite_outcome(results_url, suite_name="[CSM] Smoke")

    assert "test_suite_name=%5BCSM%5D+Smoke" in (responses.calls[0].request.url or "")
    assert seen == ["Running", "Successful"]
    assert outcome.counts == Counts(success=3, failure=0, error=0, skipped=0)
    assert outcome.status == "success"
    assert outcome.result_url == UI_RESULT_URL
    assert not outcome.rolled_up_only
    assert [test.name for test in outcome.tests] == [
        "[CSM][Smoke] Agent creates a case",
        "[CSM][Smoke] Server-created case defaults",
        "[CSM][Smoke] Customer data separation",
    ]
    assert [test.duration_seconds for test in outcome.tests] == [24.0, 7.0, 0.0]
    assert all(test.passed for test in outcome.tests)


@responses.activate
def test_failed_suite_still_yields_results() -> None:
    """Progress 'Failed' means 'ran with failing tests', not 'could not run' - no exception."""
    _register_start()
    responses.add(
        responses.GET,
        PROGRESS_URL,
        json={"result": {"status": "3", "links": {"results": {"id": "r1", "url": RESULTS_URL}}}},
    )
    _register_results(failures=2, success=1)

    client = _client()
    outcome = client.fetch_suite_outcome(_run_to_results(client), suite_name="[CSM] Smoke", per_test=False)

    assert outcome.counts.problems == 2
    assert outcome.status == "failure"
    assert outcome.rolled_up_only


@responses.activate
def test_canceled_run_raises() -> None:
    _register_start()
    responses.add(
        responses.GET,
        PROGRESS_URL,
        json={"result": {"status": "4", "status_message": "Canceled by admin"}},
    )
    client = _client()
    with pytest.raises(AtfRunError, match="[Cc]anceled"):
        client.wait_for_completion(client.start_suite(suite_name="[CSM] Smoke"), poll_interval=0, timeout=5)


@responses.activate
def test_timeout_mentions_the_client_test_runner() -> None:
    """The classic hang - UI-step suite queued with no runner online - must fail with a hint."""
    _register_start()
    responses.add(responses.GET, PROGRESS_URL, json={"result": {"status": "1", "percent_complete": "0"}})
    client = _client()
    with pytest.raises(AtfRunError, match="Client Test Runner"):
        client.wait_for_completion(client.start_suite(suite_name="[CSM] Smoke"), poll_interval=0, timeout=0)


@responses.activate
def test_unauthorized_gives_credential_hint() -> None:
    responses.add(responses.POST, RUN_URL, status=401, json={"error": "unauthorized"})
    with pytest.raises(AtfRunError, match="401.*roles"):
        _client().start_suite(suite_name="[CSM] Smoke")


@responses.activate
def test_per_test_detail_degrades_gracefully_on_403() -> None:
    """Least-privilege accounts may not read sys_atf_test_result - counts must still come through."""
    _register_start()
    responses.add(
        responses.GET,
        PROGRESS_URL,
        json={"result": {"status": "2", "links": {"results": {"id": "r1", "url": RESULTS_URL}}}},
    )
    _register_results()
    responses.add(responses.GET, TABLE_URL, status=403, body="denied")

    client = _client()
    outcome = client.fetch_suite_outcome(_run_to_results(client), suite_name="[CSM] Smoke")

    assert outcome.rolled_up_only
    assert "Per-test detail unavailable" in outcome.enrichment_note
    assert outcome.counts.total == 3


@responses.activate
def test_zero_table_rows_counts_as_unavailable_detail() -> None:
    """Zero rows most likely means the reference-field name drifted - fall back, don't report 0 tests."""
    _register_start()
    responses.add(
        responses.GET,
        PROGRESS_URL,
        json={"result": {"status": "2", "links": {"results": {"id": "r1", "url": RESULTS_URL}}}},
    )
    _register_results()
    responses.add(responses.GET, TABLE_URL, json={"result": []})

    client = _client()
    outcome = client.fetch_suite_outcome(_run_to_results(client), suite_name="[CSM] Smoke")

    assert outcome.rolled_up_only
    assert outcome.enrichment_note
    assert outcome.counts == Counts(success=3, failure=0, error=0, skipped=0)


def test_start_suite_requires_exactly_one_target() -> None:
    client = _client()
    with pytest.raises(AtfRunError, match="exactly one"):
        client.start_suite()
    with pytest.raises(AtfRunError, match="exactly one"):
        client.start_suite(suite_name="[CSM] Smoke", suite_sys_id="abc")
