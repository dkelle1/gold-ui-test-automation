"""Plain pytest unit tests (no browser, no pytest-bdd) covering the one genuinely novel piece of this
framework's user-assignment scheme: that distinct worker IDs get distinct accounts, the mapping is
stable, and it degrades sensibly outside its designed range. The C# siblings' equivalent
(`UserPoolTests`) tests a `BlockingCollection`-based lease/release pool instead - a different mechanism
entirely, needed there because their thread-based parallelism can't give each worker a fixed account for
its whole lifetime the way a separate `PYTEST_XDIST_WORKER` per worker *process* lets this one.
"""

import pytest

from saucedemo_uitests.users.assigned_user import get_assigned_user
from saucedemo_uitests.users.user_catalog import POOL_USERS


def test_assigns_a_distinct_account_to_each_of_the_pool_sized_worker_ids(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    usernames = set()
    for index in range(len(POOL_USERS)):
        monkeypatch.setenv("PYTEST_XDIST_WORKER", f"gw{index}")
        usernames.add(get_assigned_user().username)

    assert len(usernames) == len(POOL_USERS), (
        "Every concurrently-running worker must get a different account."
    )


def test_is_deterministic_same_worker_id_always_gets_the_same_account(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("PYTEST_XDIST_WORKER", "gw1")
    first = get_assigned_user().username
    second = get_assigned_user().username

    assert first == second


def test_wraps_around_via_modulo_when_there_are_more_workers_than_pooled_accounts(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("PYTEST_XDIST_WORKER", f"gw{len(POOL_USERS)}")
    assert get_assigned_user().username == POOL_USERS[0].username


def test_falls_back_to_worker_0_when_pytest_xdist_worker_is_unset(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Happens when pytest runs without `-n` (no xdist workers involved at all, so nothing sets it)."""
    monkeypatch.delenv("PYTEST_XDIST_WORKER", raising=False)
    assert get_assigned_user().username == POOL_USERS[0].username


def test_only_ever_returns_checkout_capable_accounts(monkeypatch: pytest.MonkeyPatch) -> None:
    for worker_id in range(10):
        monkeypatch.setenv("PYTEST_XDIST_WORKER", f"gw{worker_id}")
        assert get_assigned_user().can_complete_checkout is True
