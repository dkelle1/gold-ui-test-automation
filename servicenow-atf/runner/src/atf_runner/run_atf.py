"""atf-run: trigger a ServiceNow ATF suite from CI and report the verdict.

Exit codes (the pipeline contract):
  0 - suite ran, every test passed
  1 - suite ran, but has failures/errors (or ran zero tests - an empty gate must not pass)
  2 - the run itself could not be completed: configuration, auth, timeout, cancellation
"""

from __future__ import annotations

import argparse
import os
import sys
import time
from collections.abc import Sequence
from pathlib import Path

from atf_runner.junit_report import write_junit
from atf_runner.sn_client import AtfRunError, ServiceNowClient, SuiteOutcome

_PASSING_SUITE_STATUSES = {"", "success", "successful"}


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    try:
        env = {name: _require_env(name) for name in ("SN_INSTANCE_URL", "SN_USERNAME", "SN_PASSWORD")}
    except KeyError as exc:
        print(f"Missing environment variable {exc.args[0]} (see .env.example).", file=sys.stderr)
        return 2

    client = ServiceNowClient(env["SN_INSTANCE_URL"], env["SN_USERNAME"], env["SN_PASSWORD"])
    suite_label = args.suite or args.suite_sys_id
    print(f"Triggering ATF suite {suite_label!r} on {env['SN_INSTANCE_URL']}")

    started = time.monotonic()

    def report_progress(label: str, percent: int) -> None:
        elapsed = int(time.monotonic() - started)
        print(f"  [{elapsed:>4}s] {label} ({percent}%)")

    try:
        progress_url = client.start_suite(suite_name=args.suite, suite_sys_id=args.suite_sys_id)
        results_url = client.wait_for_completion(
            progress_url,
            poll_interval=args.poll_interval,
            timeout=args.timeout,
            on_progress=report_progress,
        )
        outcome = client.fetch_suite_outcome(
            results_url, suite_name=suite_label or "ATF suite", per_test=not args.no_per_test
        )
    except AtfRunError as exc:
        print(f"ATF run failed: {exc}", file=sys.stderr)
        return 2

    junit_path = Path(args.junit_out)
    write_junit(outcome, junit_path)
    _print_summary(outcome, junit_path)
    _write_github_summary(outcome)
    return 0 if _passed(outcome) else 1


def _passed(outcome: SuiteOutcome) -> bool:
    counts = outcome.counts
    return counts.problems == 0 and counts.total > 0 and outcome.status.lower() in _PASSING_SUITE_STATUSES


def _print_summary(outcome: SuiteOutcome, junit_path: Path) -> None:
    counts = outcome.counts
    print()
    print(f"Suite status : {outcome.status or '(not reported)'}")
    print(
        f"Tests        : {counts.total} total - {counts.success} passed, "
        f"{counts.failure} failed, {counts.error} errored, {counts.skipped} skipped"
    )
    if outcome.duration:
        print(f"Duration     : {outcome.duration}")
    if outcome.result_url:
        print(f"In-instance  : {outcome.result_url}")
    if outcome.enrichment_note:
        print(f"Note         : {outcome.enrichment_note}")
    print(f"JUnit        : {junit_path}")


def _write_github_summary(outcome: SuiteOutcome) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return
    counts = outcome.counts
    lines = [
        f"### ATF suite: {outcome.suite_name}",
        "",
        "| Status | Total | Passed | Failed | Errors | Skipped |",
        "|---|---|---|---|---|---|",
        f"| {outcome.status or 'n/a'} | {counts.total} | {counts.success} "
        f"| {counts.failure} | {counts.error} | {counts.skipped} |",
    ]
    if outcome.result_url:
        lines += ["", f"[Open the result in the instance]({outcome.result_url})"]
    with open(summary_path, "a", encoding="utf-8") as summary:
        summary.write("\n".join(lines) + "\n")


def _require_env(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise KeyError(name)
    return value


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="atf-run",
        description="Run a ServiceNow ATF test suite via the CI/CD REST API and write JUnit XML.",
    )
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument("--suite", help='Suite name as in the instance, e.g. "[CSM] Smoke"')
    target.add_argument("--suite-sys-id", help="Suite sys_id (alternative to --suite)")
    parser.add_argument("--junit-out", default="artifacts/atf-junit.xml", help="JUnit XML output path")
    parser.add_argument("--timeout", type=float, default=3600.0, help="Overall run timeout in seconds")
    parser.add_argument("--poll-interval", type=float, default=15.0, help="Progress poll interval in seconds")
    parser.add_argument(
        "--no-per-test",
        action="store_true",
        help="Skip the Table API read of sys_atf_test_result (least-privilege accounts)",
    )
    return parser.parse_args(argv)


if __name__ == "__main__":
    sys.exit(main())
