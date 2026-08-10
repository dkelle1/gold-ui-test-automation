"""Aggregates many :class:`CheckResult` into one pass/fail with a combined report.

The point is "run every expectation, then fail once with all the failures", not "stop at the first red".
When a load goes wrong it usually trips several checks at once, and a data engineer triaging it wants
the whole list - not to fix one, re-run the (slow) Spark suite, discover the next, and repeat.
"""

from __future__ import annotations

from collections.abc import Iterable

from dataquality.checks.result import CheckResult


class ExpectationSuite:
    def __init__(self, results: Iterable[CheckResult]):
        self.results: list[CheckResult] = list(results)

    @property
    def passed(self) -> bool:
        return all(r.passed for r in self.results)

    @property
    def failures(self) -> list[CheckResult]:
        return [r for r in self.results if not r.passed]

    def report(self) -> str:
        total = len(self.results)
        failed = len(self.failures)
        header = f"Expectation suite: {total - failed}/{total} passed"
        if not self.failures:
            return header
        lines = [f"{header}, {failed} FAILED:"]
        lines.extend(f"  - {r.message()}" for r in self.failures)
        return "\n".join(lines)


def assert_all(results: Iterable[CheckResult]) -> None:
    """pytest entry point: build the suite and assert it is all-green, with every failure in the
    message. Usage::

        assert_all([
            not_null(df, "customer_id", "silver.customers"),
            unique(df, "customer_id", "silver.customers"),
        ])
    """
    suite = ExpectationSuite(results)
    assert suite.passed, suite.report()
