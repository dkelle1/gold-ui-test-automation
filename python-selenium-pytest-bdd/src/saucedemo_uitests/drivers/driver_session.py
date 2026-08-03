"""Builds and tears down WebDriver sessions from `TestSettings`.

Relies on Selenium Manager (bundled with selenium 4.6+, confirmed installed at 4.46.0) to resolve driver
binaries automatically - no separate chromedriver/geckodriver package or manual PATH setup needed, same
as the C# sibling's own `Selenium.WebDriver` NuGet package. When `TestSettings.remote_url` is set,
sessions are created against a remote endpoint (e.g. a Selenium Grid / selenium/standalone-chrome
container) instead of a local browser, with no other code changes required.

Returns a plain `DriverSession` dataclass rather than a custom disposable wrapper class: a pytest
fixture's own `yield ...` / cleanup-after-yield structure already provides the "acquire, use, release"
lifecycle natively, so a bespoke `__enter__`/`__exit__` or IDisposable-style class here would just be
redundant ceremony - see `conftest.py`'s `driver` fixture for where `close_driver_session` actually runs.
"""

import shutil
import tempfile
from dataclasses import dataclass

from selenium import webdriver
from selenium.webdriver.chrome.options import Options as ChromeOptions
from selenium.webdriver.edge.options import Options as EdgeOptions
from selenium.webdriver.firefox.options import Options as FirefoxOptions
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.config.settings import TestSettings


@dataclass
class DriverSession:
    driver: WebDriver
    user_data_dir: str | None


def _create_user_data_dir() -> str:
    return tempfile.mkdtemp(prefix="saucedemo-uitests-")


def _build_chrome_options(settings: TestSettings, user_data_dir: str) -> ChromeOptions:
    options = ChromeOptions()
    options.add_argument(f"--user-data-dir={user_data_dir}")
    options.add_argument("--window-size=1920,1080")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")

    if settings.headless:
        options.add_argument("--headless=new")
        options.add_argument("--disable-gpu")

    return options


def _build_edge_options(settings: TestSettings, user_data_dir: str) -> EdgeOptions:
    options = EdgeOptions()
    options.add_argument(f"--user-data-dir={user_data_dir}")
    options.add_argument("--window-size=1920,1080")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")

    if settings.headless:
        options.add_argument("--headless=new")

    return options


def _build_firefox_options(settings: TestSettings) -> FirefoxOptions:
    options = FirefoxOptions()

    if settings.headless:
        # Mirrors Chrome/Edge's --window-size: Maximize() doesn't reliably resize a headless viewport,
        # so headless Firefox would otherwise default to a small window.
        options.add_argument("-headless")
        options.add_argument("-width=1920")
        options.add_argument("-height=1080")

    return options


def create_driver_session(settings: TestSettings) -> DriverSession:
    user_data_dir: str | None = None

    if settings.browser == "chrome":
        user_data_dir = _create_user_data_dir()
        options: ChromeOptions | EdgeOptions | FirefoxOptions = _build_chrome_options(settings, user_data_dir)
    elif settings.browser == "edge":
        user_data_dir = _create_user_data_dir()
        options = _build_edge_options(settings, user_data_dir)
    elif settings.browser == "firefox":
        options = _build_firefox_options(settings)
    else:
        raise ValueError(f"Unsupported browser: {settings.browser}")

    driver = _create_local_or_remote_driver(settings, options)

    try:
        driver.set_page_load_timeout(settings.page_load_timeout_seconds)

        if not settings.headless:
            # Headless browsers are sized by the explicit window-size arguments above instead.
            # maximize_window() in headless Chrome/Edge does not enlarge anything - it resizes the
            # window to the headless environment's virtual screen, silently *undoing*
            # --window-size=1920,1080 - the same trap the Selenium C# sibling's README documents
            # (confirmed there from real CI screenshots, not re-verified independently here since this
            # sandbox cannot launch a version-matched Chrome/chromedriver pair at all - see this
            # framework's own README "Known limitations").
            driver.maximize_window()
    except Exception:
        # The session already exists at this point - if post-launch setup fails, quit it rather than
        # leaking a live browser process/remote session that nothing will ever dispose.
        driver.quit()
        raise

    return DriverSession(driver=driver, user_data_dir=user_data_dir)


def _create_local_or_remote_driver(
    settings: TestSettings, options: ChromeOptions | EdgeOptions | FirefoxOptions
) -> WebDriver:
    if settings.remote_url:
        return webdriver.Remote(command_executor=settings.remote_url, options=options)

    if isinstance(options, ChromeOptions):
        return webdriver.Chrome(options=options)
    if isinstance(options, EdgeOptions):
        return webdriver.Edge(options=options)
    if isinstance(options, FirefoxOptions):
        return webdriver.Firefox(options=options)
    raise ValueError(f"Unsupported options type: {type(options)}")


def close_driver_session(session: DriverSession) -> None:
    try:
        session.driver.quit()
    finally:
        if session.user_data_dir:
            shutil.rmtree(session.user_data_dir, ignore_errors=True)
