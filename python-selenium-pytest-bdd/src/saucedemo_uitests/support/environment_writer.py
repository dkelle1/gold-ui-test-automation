"""Writes Allure's environment.properties file into the results directory so the report's "Environment"
tab shows what this run actually exercised (base URL, browser, worker/pool sizing, CI context).

Unlike the C# sibling (where Allure.Reqnroll always writes allure-results to a fixed,
build-output-relative path), allure-pytest's results directory is whatever `--alluredir` was pointed at
for this invocation - so the directory is resolved from pytest's own `Config.option.allure_report_dir` by
the caller (see `conftest.py`'s `pytest_configure` hook) rather than assumed here.
"""

import os
import platform
from importlib.metadata import version
from pathlib import Path

from saucedemo_uitests.config.parallel_settings import MAX_PARALLEL_WORKERS
from saucedemo_uitests.config.settings import TestSettings
from saucedemo_uitests.users.user_catalog import POOL_USERS


def write_environment_file(settings: TestSettings, results_dir: Path) -> None:
    results_dir.mkdir(parents=True, exist_ok=True)

    is_ci = os.environ.get("CI", "").lower() == "true"

    lines = [
        f"BaseUrl={settings.base_url}",
        f"Browser={settings.browser}",
        f"Headless={settings.headless}",
        f"Workers={MAX_PARALLEL_WORKERS}",
        f"UserPoolSize={len(POOL_USERS)}",
        f"SeleniumVersion={version('selenium')}",
        f"OS={platform.platform()}",
        f"CI={is_ci}",
        f"GITHUB_RUN_ID={os.environ.get('GITHUB_RUN_ID', '')}",
    ]

    (results_dir / "environment.properties").write_text("\n".join(lines) + "\n", encoding="utf-8")
