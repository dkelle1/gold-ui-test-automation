"""SuiteOutcome -> JUnit XML, the lingua franca both CI systems here already ingest.

Two shapes, matching what the API actually gave us:
- per-test detail available -> one <testcase> per ATF test, real names and durations;
- rolled-up only -> a single synthetic <testcase> carrying the aggregate verdict, so the build
  still turns red/green correctly instead of reporting "0 tests".
"""

from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

from atf_runner.sn_client import SuiteOutcome, TestOutcome


def write_junit(outcome: SuiteOutcome, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tree = ET.ElementTree(build_junit(outcome))
    ET.indent(tree)
    tree.write(path, encoding="utf-8", xml_declaration=True)


def build_junit(outcome: SuiteOutcome) -> ET.Element:
    counts = outcome.counts
    suite = ET.Element(
        "testsuite",
        {
            "name": outcome.suite_name,
            "tests": str(counts.total),
            "failures": str(counts.failure),
            "errors": str(counts.error),
            "skipped": str(counts.skipped),
        },
    )
    if outcome.tests:
        total_seconds = sum(test.duration_seconds for test in outcome.tests)
        suite.set("time", f"{total_seconds:.3f}")
        for test in outcome.tests:
            suite.append(_testcase(outcome.suite_name, test))
    else:
        suite.append(_rolled_up_testcase(outcome))
    if outcome.result_url or outcome.enrichment_note:
        system_out = ET.SubElement(suite, "system-out")
        lines = []
        if outcome.result_url:
            lines.append(f"In-instance result: {outcome.result_url}")
        if outcome.enrichment_note:
            lines.append(outcome.enrichment_note)
        system_out.text = "\n".join(lines)
    return suite


def _testcase(suite_name: str, test: TestOutcome) -> ET.Element:
    case = ET.Element(
        "testcase",
        {"classname": suite_name, "name": test.name, "time": f"{test.duration_seconds:.3f}"},
    )
    if test.passed:
        return case
    if test.skipped:
        ET.SubElement(case, "skipped", {"message": _first_line(test.output) or "skipped"})
        return case
    tag = "error" if test.errored else "failure"
    node = ET.SubElement(case, tag, {"message": _first_line(test.output) or test.status})
    node.text = test.output
    return case


def _rolled_up_testcase(outcome: SuiteOutcome) -> ET.Element:
    counts = outcome.counts
    case = ET.Element(
        "testcase",
        {"classname": outcome.suite_name, "name": f"{outcome.suite_name} (rolled-up result)"},
    )
    if counts.problems or counts.total == 0:
        message = (
            f"{counts.failure} failed, {counts.error} errored out of {counts.total} tests "
            f"(suite status: {outcome.status or 'unknown'}). Drill down in the instance."
            if counts.total
            else "The suite ran zero tests - an empty gate must not pass."
        )
        ET.SubElement(case, "failure", {"message": message})
    return case


def _first_line(text: str) -> str:
    return text.strip().splitlines()[0].strip() if text.strip() else ""
