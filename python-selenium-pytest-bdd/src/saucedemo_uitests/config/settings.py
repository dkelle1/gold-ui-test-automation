"""Bound from `TestSettings` in appsettings*.json, overridable by the plain env vars named below."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Literal, TypeGuard
from urllib.parse import urlparse

BrowserName = Literal["chrome", "firefox", "edge"]


@dataclass(frozen=True)
class TestSettings:
    base_url: str
    browser: BrowserName
    headless: bool
    # A Selenium Grid / remote WebDriver endpoint. None means launch a local browser.
    remote_url: str | None
    explicit_wait_seconds: int
    page_load_timeout_seconds: int


def _read_json_config(file_name: str) -> dict[str, object]:
    path = Path.cwd() / file_name
    if not path.exists():
        return {}

    with path.open(encoding="utf-8") as f:
        parsed = json.load(f)
    settings = parsed.get("TestSettings", {})
    return settings if isinstance(settings, dict) else {}


def _is_browser_name(value: str) -> TypeGuard[BrowserName]:
    return value in ("chrome", "firefox", "edge")


def _int_setting(env_value: str | None, config_value: object, default: int) -> int:
    if env_value is not None:
        return int(env_value)
    if config_value is None:
        return default
    return int(str(config_value))


def _load_settings() -> TestSettings:
    is_ci = os.environ.get("CI") == "true"

    base = _read_json_config("appsettings.json")
    # appsettings.ci.json overrides base settings whenever CI=true, exactly like the other two
    # frameworks - layered in before env vars, which stay the highest-precedence override either way.
    ci_overrides = _read_json_config("appsettings.ci.json") if is_ci else {}
    merged: dict[str, object] = {**base, **ci_overrides}

    browser = os.environ.get("BROWSER") or str(merged.get("Browser", "chrome"))
    if not _is_browser_name(browser):
        raise ValueError(f'Unsupported browser "{browser}" - expected chrome, firefox, or edge.')

    base_url = os.environ.get("BASE_URL") or merged.get("BaseUrl")
    if not base_url or not isinstance(base_url, str):
        raise ValueError(
            "TestSettings.BaseUrl must be configured (appsettings.json or the BASE_URL env var)."
        )
    if not urlparse(base_url).scheme:
        raise ValueError(f'TestSettings.BaseUrl is not a valid absolute URL: "{base_url}".')

    headless_env = os.environ.get("HEADLESS")
    headless = headless_env == "true" if headless_env is not None else bool(merged.get("Headless", False))

    remote_env = os.environ.get("REMOTE_URL")
    config_remote = merged.get("RemoteUrl")
    remote_url = remote_env or (str(config_remote) if config_remote else None)

    return TestSettings(
        base_url=base_url,
        browser=browser,
        headless=headless,
        remote_url=remote_url,
        explicit_wait_seconds=_int_setting(
            os.environ.get("EXPLICIT_WAIT_SECONDS"), merged.get("ExplicitWaitSeconds"), default=20
        ),
        page_load_timeout_seconds=_int_setting(
            os.environ.get("PAGE_LOAD_TIMEOUT_SECONDS"), merged.get("PageLoadTimeoutSeconds"), default=30
        ),
    )


@lru_cache(maxsize=1)
def get_settings() -> TestSettings:
    """Loaded once and cached for the process lifetime - settings never change mid-run."""
    return _load_settings()
