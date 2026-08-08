"""junit_report: both output shapes (per-test detail and rolled-up-only) parsed back with ElementTree."""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

from atf_runner.junit_report import write_junit
from atf_runner.sn_client import Counts, SuiteOutcome, TestOutcome

UI_URL = "https://dev1.example.service-now.com/sys_atf_test_suite_result.do?sys_id=sr1"


def _detailed_outcome() -> SuiteOutcome:
    return SuiteOutcome(
        suite_name="[CSM] Regression",
        status="failure",
        duration="00:02:10",
        counts=Counts(success=1, failure=1, error=1, skipped=1),
        result_url=UI_URL,
        tests=(
            TestOutcome("[CSM][Regression] Lifecycle", "success", 24.0, ""),
            TestOutcome(
                "[CSM][Regression] Negative guard",
                "failure",
                1.5,
                "Assertion failed: state expected New\ndetail with <angle> & ampersand",
            ),
            TestOutcome("[CSM][Regression] Skipped one", "skipped", 0.0, "runner offline"),
            TestOutcome("[CSM][Regression] Errored one", "error", 0.0, "Unexpected server error"),
        ),
    )


def test_per_test_detail_report(tmp_path: Path) -> None:
    junit_path = tmp_path / "nested" / "atf-junit.xml"  # parent dir must be created by the writer
    write_junit(_detailed_outcome(), junit_path)

    suite = ET.parse(junit_path).getroot()
    assert suite.tag == "testsuite"
    assert (suite.get("tests"), suite.get("failures"), suite.get("errors"), suite.get("skipped")) == (
        "4",
        "1",
        "1",
        "1",
    )
    cases = suite.findall("testcase")
    assert [case.get("name") for case in cases] == [
        "[CSM][Regression] Lifecycle",
        "[CSM][Regression] Negative guard",
        "[CSM][Regression] Skipped one",
        "[CSM][Regression] Errored one",
    ]
    assert all(case.get("classname") == "[CSM] Regression" for case in cases)

    passed, failed, skipped, errored = cases
    assert passed.find("failure") is None and passed.get("time") == "24.000"
    failure = failed.find("failure")
    assert failure is not None
    assert failure.get("message") == "Assertion failed: state expected New"
    # ElementTree escaping round-trips the raw output, angle brackets and ampersand included.
    assert failure.text == "Assertion failed: state expected New\ndetail with <angle> & ampersand"
    assert skipped.find("skipped") is not None
    assert errored.find("error") is not None

    system_out = suite.find("system-out")
    assert system_out is not None and UI_URL in (system_out.text or "")


def test_rolled_up_failure_report(tmp_path: Path) -> None:
    outcome = SuiteOutcome(
        suite_name="[CSM] Smoke",
        status="failure",
        duration="",
        counts=Counts(success=4, failure=2, error=0, skipped=0),
        result_url="",
        tests=(),
        enrichment_note="Per-test detail unavailable, JUnit holds the rolled-up result only: 403",
    )
    junit_path = tmp_path / "junit.xml"
    write_junit(outcome, junit_path)

    suite = ET.parse(junit_path).getroot()
    assert suite.get("tests") == "6"
    cases = suite.findall("testcase")
    assert len(cases) == 1
    failure = cases[0].find("failure")
    assert failure is not None and "2 failed" in (failure.get("message") or "")
    system_out = suite.find("system-out")
    assert system_out is not None and "Per-test detail unavailable" in (system_out.text or "")


def test_rolled_up_pass_report(tmp_path: Path) -> None:
    outcome = SuiteOutcome(
        suite_name="[CSM] Smoke",
        status="success",
        duration="00:00:40",
        counts=Counts(success=5, failure=0, error=0, skipped=0),
        result_url=UI_URL,
        tests=(),
    )
    junit_path = tmp_path / "junit.xml"
    write_junit(outcome, junit_path)

    suite = ET.parse(junit_path).getroot()
    cases = suite.findall("testcase")
    assert len(cases) == 1
    assert cases[0].find("failure") is None


def test_zero_tests_is_a_failing_report(tmp_path: Path) -> None:
    """An empty suite result must produce a red JUnit - a gate that ran nothing is not green."""
    outcome = SuiteOutcome(
        suite_name="[CSM] Smoke",
        status="success",
        duration="",
        counts=Counts(success=0, failure=0, error=0, skipped=0),
        result_url="",
        tests=(),
    )
    junit_path = tmp_path / "junit.xml"
    write_junit(outcome, junit_path)

    suite = ET.parse(junit_path).getroot()
    failure = suite.findall("testcase")[0].find("failure")
    assert failure is not None and "zero tests" in (failure.get("message") or "")
