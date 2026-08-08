"""Client for the ServiceNow CI/CD REST API (ATF suite runs) plus a Table API result read.

The three-call contract this wraps (docs/architecture.md has the sequence diagram):

1. ``POST /api/sn_cicd/testsuite/run?test_suite_name=...`` -> ``result.links.progress.url``
2. ``GET  <progress url>`` until ``result.status`` is terminal:
   ``"0"`` Pending / ``"1"`` Running / ``"2"`` Successful / ``"3"`` Failed / ``"4"`` Canceled.
   "Failed" here includes "the suite ran and tests failed" - results still exist, so it is a
   completion, not an error; only Canceled / timeout / missing results link are errors.
3. ``GET  <results url>`` -> rolled-up counts (serialized as *strings* by the API) and the
   ``sys_atf_test_suite_result`` sys_id, which feeds an optional Table API read of
   ``sys_atf_test_result`` for per-test JUnit detail. That read is best-effort: least-privilege
   service accounts may not see the result tables, and the field layout can drift between
   releases - either way the caller still gets the rolled-up outcome.
"""

from __future__ import annotations

import re
import time
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from datetime import datetime
from typing import Any

import requests

PROGRESS_LABELS = {
    "0": "Pending",
    "1": "Running",
    "2": "Successful",
    "3": "Failed",
    "4": "Canceled",
}
_TERMINAL_WITH_RESULTS = {"2", "3"}
_CANCELED = "4"

_PASS_STATUSES = {"success", "successful"}
_SKIP_STATUSES = {"skipped"}
_ERROR_STATUSES = {"error"}


class AtfRunError(RuntimeError):
    """The suite run could not be completed: auth, timeout, cancellation, or a malformed response."""


@dataclass(frozen=True)
class Counts:
    success: int
    failure: int
    error: int
    skipped: int

    @property
    def total(self) -> int:
        return self.success + self.failure + self.error + self.skipped

    @property
    def problems(self) -> int:
        return self.failure + self.error


@dataclass(frozen=True)
class TestOutcome:
    name: str
    status: str  # raw sys_atf_test_result status: success / failure / skipped / error
    duration_seconds: float
    output: str

    @property
    def passed(self) -> bool:
        return self.status.lower() in _PASS_STATUSES

    @property
    def skipped(self) -> bool:
        return self.status.lower() in _SKIP_STATUSES

    @property
    def errored(self) -> bool:
        return self.status.lower() in _ERROR_STATUSES


@dataclass(frozen=True)
class SuiteOutcome:
    suite_name: str
    status: str  # test_suite_status from the results payload ("" if the API omitted it)
    duration: str
    counts: Counts
    result_url: str  # in-instance link to the sys_atf_test_suite_result ("" if absent)
    tests: tuple[TestOutcome, ...]
    enrichment_note: str = ""  # non-empty when per-test detail was requested but unavailable

    @property
    def rolled_up_only(self) -> bool:
        return not self.tests


class ServiceNowClient:
    def __init__(
        self,
        base_url: str,
        username: str,
        password: str,
        *,
        request_timeout: float = 60.0,
        session: requests.Session | None = None,
    ) -> None:
        self._base = base_url.rstrip("/")
        self._request_timeout = request_timeout
        self._session = session or requests.Session()
        self._session.auth = (username, password)
        self._session.headers.update({"Accept": "application/json"})

    def start_suite(self, *, suite_name: str | None = None, suite_sys_id: str | None = None) -> str:
        """Trigger a suite run; returns the progress URL to poll."""
        if bool(suite_name) == bool(suite_sys_id):
            raise AtfRunError("Provide exactly one of suite_name / suite_sys_id.")
        params = {"test_suite_name": suite_name} if suite_name else {"test_suite_sys_id": suite_sys_id}
        result = self._request("POST", f"{self._base}/api/sn_cicd/testsuite/run", params=params)
        progress_url = _link(result, "progress", "url")
        if not progress_url:
            raise AtfRunError(f"Run accepted but no progress link returned: {result!r}")
        return progress_url

    def wait_for_completion(
        self,
        progress_url: str,
        *,
        poll_interval: float,
        timeout: float,
        on_progress: Callable[[str, int], None] | None = None,
    ) -> str:
        """Poll the progress record until terminal; returns the results URL.

        Raises AtfRunError on timeout, cancellation, or a terminal state with no results link -
        the "no Client Test Runner online" failure mode surfaces here as the timeout.
        """
        deadline = time.monotonic() + timeout
        while True:
            result = self._request("GET", progress_url)
            status = str(result.get("status", ""))
            label = PROGRESS_LABELS.get(status, str(result.get("status_label", status)))
            if on_progress is not None:
                on_progress(label, _to_int(result.get("percent_complete")))
            if status == _CANCELED:
                raise AtfRunError(f"Suite run canceled by the instance: {_detail(result)}")
            if status in _TERMINAL_WITH_RESULTS:
                results_url = _link(result, "results", "url")
                if not results_url:
                    raise AtfRunError(
                        f"Run finished ({label}) but returned no results link: {_detail(result)}"
                    )
                return results_url
            if time.monotonic() >= deadline:
                raise AtfRunError(
                    f"Timed out after {timeout:.0f}s waiting for the suite to finish (last status: {label}). "
                    "If the suite contains form/portal steps, check that a Scheduled Client Test Runner "
                    "is online for the instance."
                )
            time.sleep(poll_interval)

    def fetch_suite_outcome(
        self, results_url: str, *, suite_name: str, per_test: bool = True
    ) -> SuiteOutcome:
        result = self._request("GET", results_url)
        counts = Counts(
            success=_to_int(result.get("rolled_up_test_success_count")),
            failure=_to_int(result.get("rolled_up_test_failure_count")),
            error=_to_int(result.get("rolled_up_test_error_count")),
            skipped=_to_int(result.get("rolled_up_test_skip_count")),
        )
        suite_result_sys_id = _link(result, "results", "id")
        tests: tuple[TestOutcome, ...] = ()
        note = ""
        if per_test and suite_result_sys_id:
            try:
                tests = self._fetch_test_results(suite_result_sys_id)
            except AtfRunError as exc:
                note = f"Per-test detail unavailable, JUnit holds the rolled-up result only: {exc}"
        elif per_test:
            note = "Per-test detail unavailable: results payload carried no suite-result sys_id."
        return SuiteOutcome(
            suite_name=suite_name,
            status=str(result.get("test_suite_status", "")),
            duration=str(result.get("test_suite_duration", "")),
            counts=counts,
            result_url=_link(result, "results", "url"),
            tests=tests,
            enrichment_note=note,
        )

    def _fetch_test_results(self, suite_result_sys_id: str) -> tuple[TestOutcome, ...]:
        params = {
            # Field name as of current releases; if it drifts, the zero-row guard below turns the
            # drift into a graceful rolled-up-only report instead of a wrong "0 tests ran" JUnit.
            "sysparm_query": f"test_suite_result={suite_result_sys_id}^ORDERBYsys_created_on",
            "sysparm_display_value": "all",
            "sysparm_fields": "test,status,duration,output",
            "sysparm_limit": "1000",
        }
        payload = self._request_raw("GET", f"{self._base}/api/now/table/sys_atf_test_result", params=params)
        rows = payload.get("result")
        if not isinstance(rows, list) or not rows:
            raise AtfRunError("Table API returned no sys_atf_test_result rows for the suite result.")
        outcomes = []
        for row in rows:
            if not isinstance(row, Mapping):
                continue
            outcomes.append(
                TestOutcome(
                    name=_field(row, "test", "display_value") or "(unnamed test)",
                    status=_field(row, "status", "value") or "unknown",
                    duration_seconds=_parse_duration(_field(row, "duration", "value")),
                    output=_field(row, "output", "value"),
                )
            )
        if not outcomes:
            raise AtfRunError("Table API rows for sys_atf_test_result had an unexpected shape.")
        return tuple(outcomes)

    def _request(
        self, method: str, url: str, *, params: Mapping[str, str | None] | None = None
    ) -> dict[str, Any]:
        payload = self._request_raw(method, url, params=params)
        result = payload.get("result")
        if not isinstance(result, dict):
            raise AtfRunError(f"{method} {url} returned no result object: {payload!r}")
        return result

    def _request_raw(
        self, method: str, url: str, *, params: Mapping[str, str | None] | None = None
    ) -> dict[str, Any]:
        try:
            response = self._session.request(method, url, params=params, timeout=self._request_timeout)
        except requests.RequestException as exc:
            raise AtfRunError(f"{method} {url} failed: {exc}") from exc
        if response.status_code == 401:
            raise AtfRunError(
                f"{method} {url} -> HTTP 401. Check SN_USERNAME/SN_PASSWORD and that the account "
                "holds the CI/CD automation + ATF roles (docs/architecture.md#roles--security)."
            )
        if response.status_code >= 400:
            raise AtfRunError(f"{method} {url} -> HTTP {response.status_code}: {response.text[:300]}")
        try:
            payload: Any = response.json()
        except ValueError as exc:
            raise AtfRunError(f"{method} {url} returned non-JSON: {response.text[:300]}") from exc
        if not isinstance(payload, dict):
            raise AtfRunError(f"{method} {url} returned unexpected JSON shape: {payload!r}")
        return payload


def _link(result: Mapping[str, Any], link_name: str, key: str) -> str:
    links = result.get("links")
    if not isinstance(links, Mapping):
        return ""
    link = links.get(link_name)
    if not isinstance(link, Mapping):
        return ""
    value = link.get(key)
    return str(value) if value else ""


def _detail(result: Mapping[str, Any]) -> str:
    parts = [str(result.get(k)) for k in ("status_message", "status_detail", "error") if result.get(k)]
    return "; ".join(parts) or "(no detail provided)"


def _to_int(value: object) -> int:
    """The CI/CD API serializes numbers as strings ("2"); absent/garbage values count as 0."""
    try:
        return int(str(value))
    except (TypeError, ValueError):
        return 0


def _field(row: Mapping[str, Any], name: str, key: str) -> str:
    """Read a Table API field under sysparm_display_value=all ({value, display_value} dicts),
    tolerating plain-string fields in case the instance ignores the parameter."""
    raw = row.get(name)
    if isinstance(raw, Mapping):
        value = raw.get(key) or raw.get("display_value") or raw.get("value")
    else:
        value = raw
    return "" if value is None else str(value)


def _parse_duration(value: str) -> float:
    """glide_duration raw values look like '1970-01-01 00:00:24' (epoch-anchored); older layouts
    surface 'HH:MM:SS'. Anything else parses to 0.0 - duration is cosmetic in JUnit, never worth
    failing a build over."""
    if not value:
        return 0.0
    try:
        parsed = datetime.strptime(value, "%Y-%m-%d %H:%M:%S")
        return (parsed - datetime(1970, 1, 1)).total_seconds()
    except ValueError:
        pass
    match = re.fullmatch(r"(\d+):(\d{2}):(\d{2})", value)
    if match:
        hours, minutes, seconds = (int(part) for part in match.groups())
        return float(hours * 3600 + minutes * 60 + seconds)
    return 0.0
