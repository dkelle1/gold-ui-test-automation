"""Deterministic per-worker user assignment.

pytest-xdist workers are separate OS processes (confirmed directly: each worker's `PYTEST_XDIST_WORKER`
env var is only ever visible to that one process), unlike the C# siblings' NUnit workers (threads within
one process, requiring `UserPool`'s `BlockingCollection`-based lease/release to avoid two threads racing
for the same account) or ts-playwright-cucumber's `worker_threads` (also single-process). Two pytest-xdist
worker processes could never share a mutable in-memory pool to lease from even if we wanted them to - so
a fixed index-based mapping, one account per worker for its whole lifetime, is not just simpler than a
lease/release pool, it is the only model that makes sense here at all.
"""

import os

from saucedemo_uitests.config.parallel_settings import MAX_PARALLEL_WORKERS
from saucedemo_uitests.users.user_account import UserAccount
from saucedemo_uitests.users.user_catalog import POOL_USERS

if len(POOL_USERS) < MAX_PARALLEL_WORKERS:
    raise RuntimeError(
        f"POOL_USERS has {len(POOL_USERS)} account(s) but MAX_PARALLEL_WORKERS is {MAX_PARALLEL_WORKERS}. "
        "Every parallel worker needs its own account, or two workers would collide on the same login "
        "session."
    )


def _worker_index() -> int:
    """`PYTEST_XDIST_WORKER` is "gw{N}" for an xdist worker (confirmed directly against a real 3-worker
    run), or unset when running without `-n` at all - `gw0` in that case, matching a single worker."""
    worker_id = os.environ.get("PYTEST_XDIST_WORKER", "gw0")
    return int(worker_id.removeprefix("gw"))


def get_assigned_user() -> UserAccount:
    index = _worker_index() % len(POOL_USERS)
    return POOL_USERS[index]
