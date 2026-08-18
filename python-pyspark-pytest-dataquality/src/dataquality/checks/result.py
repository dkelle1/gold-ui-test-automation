"""The single value every data-quality check returns.

Checks return a structured result rather than asserting directly, for three reasons a QA lead cares
about: (1) a whole expectation suite can run and report *all* failures at once instead of stopping at
the first, (2) the same check objects can feed a dashboard or a data-contract gate outside pytest, and
(3) the failure message carries the offending count and a sample of bad keys, so triage does not start
with "now go write a query to find out which rows".
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass(frozen=True)
class CheckResult:
    check: str
    """Stable name of the check, e.g. "not_null(email)". Used as the pytest assertion message header."""

    passed: bool

    dataset: str
    """Which layer/table this ran against, e.g. "silver.customers"."""

    observed: int = 0
    """The count that decided the result - failing rows, duplicate rows, mismatched rows, etc."""

    details: str = ""

    sample: list[Any] = field(default_factory=list)
    """A few offending keys/values, for triage. Never the whole failing set - bounded on purpose."""

    def message(self) -> str:
        status = "PASSED" if self.passed else "FAILED"
        head = f"[{status}] {self.dataset}: {self.check}"
        if self.passed:
            return head
        parts = [head, f"observed={self.observed}"]
        if self.details:
            parts.append(self.details)
        if self.sample:
            parts.append(f"sample={self.sample}")
        return " | ".join(parts)
