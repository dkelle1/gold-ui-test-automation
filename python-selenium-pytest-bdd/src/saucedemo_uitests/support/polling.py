"""Polls an arbitrary zero-arg callable until it returns the expected value or a timeout elapses - the
Python/Selenium equivalent of the C# sibling's NUnit `Assert.That(() => ..., Is.EqualTo(x).After(ms, ms))`
polling assertions used for state that updates asynchronously (e.g. a cart badge re-rendering after a
click).

Deliberately a plain time.monotonic() loop with a direct `==` check rather than built on
`WebDriverWait.until` (as `pages/base_page.py` is): `until` stops as soon as its callback returns
anything falsy-looking as "not ready" - which would misfire for a legitimately expected falsy value (e.g.
asserting a count of exactly 0), so a direct equality check is the correct general-purpose contract here,
not just a stylistic difference.
"""

import time
from collections.abc import Callable
from typing import TypeVar

_T = TypeVar("_T")

_DEFAULT_TIMEOUT_SECONDS = 5.0
_DEFAULT_POLL_SECONDS = 0.25


def assert_eventually(
    read: Callable[[], _T],
    expected: _T,
    description: str,
    *,
    timeout_seconds: float = _DEFAULT_TIMEOUT_SECONDS,
    poll_seconds: float = _DEFAULT_POLL_SECONDS,
) -> None:
    deadline = time.monotonic() + timeout_seconds
    actual = read()

    while actual != expected and time.monotonic() < deadline:
        time.sleep(poll_seconds)
        actual = read()

    assert actual == expected, f"{description}: expected {expected!r}, got {actual!r} after polling."
