"""Parses typed data out of Gherkin `@tag` values on the current scenario.

pytest-bdd's default `pytest_bdd_apply_tag` hook turns every Gherkin tag into a pytest marker of the
same name (confirmed directly, including tags containing a colon like `user:locked_out_user`), so
reading one back is a matter of inspecting the current test item's markers - no pytest-bdd-specific API
needed.
"""

import pytest

from saucedemo_uitests.users.user_account import UserAccount
from saucedemo_uitests.users.user_catalog import user_by_username

_USER_TAG_PREFIX = "user:"


def get_tagged_user(request: pytest.FixtureRequest) -> UserAccount | None:
    """Returns the account named by a `@user:<username>` tag on the scenario, if present. Lets a
    scenario bypass the per-worker assignment and target one specific (often deliberately broken)
    saucedemo account directly, e.g. `@user:locked_out_user`."""
    marker = next(
        (m for m in request.node.iter_markers() if m.name.startswith(_USER_TAG_PREFIX)),
        None,
    )
    if marker is None:
        return None

    return user_by_username(marker.name.removeprefix(_USER_TAG_PREFIX))
