"""Project-wide pytest configuration: per-scenario fixture lifecycle (the pytest-bdd equivalent of the
C# sibling's `ScenarioHooks`/`TestRunHooks`), plus the one place every step-definition module must be
imported.

pytest-bdd resolves a step by scanning the *defining* module's own namespace for `@given`/`@when`/`@then`
-decorated functions (confirmed directly: importing a steps module elsewhere via a plain `import` does
NOT make its steps available - only binding its public names directly into a conftest.py's own namespace
via `from ... import *` does). Since conftest.py is loaded for every test file in this project regardless
of which package under `tests/` collects it, star-importing every steps module here once is what makes
all of them available to every `.feature` file, matching how the two C# frameworks' step-definition
classes are all discoverable process-wide without each feature file wiring up its own subset.
"""

import os
from collections.abc import Iterator
from pathlib import Path

import allure
import pytest
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.config.settings import get_settings
from saucedemo_uitests.drivers.driver_session import close_driver_session, create_driver_session

# Each of these four looks unused to a linter - nothing in this file calls their names directly, pytest-
# bdd's own step lookup is the only consumer, via the names `import *` binds into this module's namespace.
from saucedemo_uitests.steps.cart_steps import *  # noqa: F401,F403
from saucedemo_uitests.steps.checkout_steps import *  # noqa: F401,F403
from saucedemo_uitests.steps.inventory_steps import *  # noqa: F401,F403
from saucedemo_uitests.steps.login_steps import *  # noqa: F401,F403
from saucedemo_uitests.support.allure_attachments import attach_failure_evidence
from saucedemo_uitests.support.environment_writer import write_environment_file
from saucedemo_uitests.users.assigned_user import get_assigned_user
from saucedemo_uitests.users.tag_helpers import get_tagged_user
from saucedemo_uitests.users.user_account import UserAccount


def pytest_configure(config: pytest.Config) -> None:
    # Touch settings here (not just lazily on first fixture use) so a misconfigured BaseUrl fails the
    # whole run immediately instead of surfacing as a confusing per-scenario failure later.
    settings = get_settings()

    results_dir = config.option.allure_report_dir
    if results_dir:
        # Written at configure time, not at session finish, so the file (and its CI/browser/worker
        # context) still exists in the results directory even if the run crashes or is cancelled before
        # finishing.
        write_environment_file(settings, Path(results_dir))


@pytest.hookimpl(wrapper=True)
def pytest_runtest_makereport(item: pytest.Item, call: pytest.CallInfo[None]) -> object:
    """Stashes each phase's outcome onto the item (as `rep_setup`/`rep_call`/`rep_teardown`) so the
    `driver` fixture's teardown can tell whether the scenario body actually failed - fixture teardown
    code has no other way to observe that, since it runs regardless of the test's outcome."""
    result = yield
    setattr(item, f"rep_{call.when}", result)
    return result


@pytest.fixture
def user(request: pytest.FixtureRequest) -> UserAccount:
    """The account this scenario runs as: a `@user:<username>`-tagged account if the scenario specifies
    one, otherwise this worker's fixed pool assignment."""
    tagged_user = get_tagged_user(request)
    return tagged_user if tagged_user is not None else get_assigned_user()


@pytest.fixture
def driver(request: pytest.FixtureRequest, user: UserAccount) -> Iterator[WebDriver]:
    """A fresh browser per scenario (matching both C# siblings, not ts-playwright-cucumber's
    worker-shared-browser optimisation - pytest-xdist workers are separate processes with nothing to
    share a browser instance across in the first place)."""
    allure.dynamic.parameter("user", user.username)
    allure.dynamic.parameter("worker", os.environ.get("PYTEST_XDIST_WORKER", "single-threaded"))

    settings = get_settings()
    session = create_driver_session(settings)

    try:
        session.driver.get(settings.base_url)
    except Exception:
        # Setup failed after the browser process was already launched - close it here, since a fixture
        # that raises before yielding never reaches its own teardown code below.
        close_driver_session(session)
        raise

    yield session.driver

    try:
        rep_call = getattr(request.node, "rep_call", None)
        if rep_call is not None and rep_call.failed:
            attach_failure_evidence(session.driver)
    finally:
        close_driver_session(session)
