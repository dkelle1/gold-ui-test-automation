"""Attaches failure evidence to the current Allure test result. allure-pytest's reporter tracks the
active test/step purely through pytest's own (per-item, sequential) hook lifecycle rather than any
thread-local or async-local context, so - unlike the C# sibling's note about `Allure.Net.Commons`'
AsyncLocal-based safety under concurrent scenarios - concurrency isn't a consideration here at all:
pytest-xdist parallelism is separate worker *processes* (see `users.assigned_user`), each running its own
sequential pytest session with its own independent allure-pytest state.
"""

import allure
from selenium.common.exceptions import WebDriverException
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.support.screenshot_helper import capture_screenshot_png


def attach_failure_evidence(driver: WebDriver) -> None:
    allure.attach(
        capture_screenshot_png(driver), name="failure-screenshot", attachment_type=allure.attachment_type.PNG
    )
    allure.attach(driver.page_source, name="page-source", attachment_type=allure.attachment_type.HTML)
    allure.attach(driver.current_url, name="final-url", attachment_type=allure.attachment_type.TEXT)

    _try_attach_browser_console_log(driver)


def _try_attach_browser_console_log(driver: WebDriver) -> None:
    """`get_log` is only defined on Chrome/Edge's `ChromiumDriver` (confirmed directly: it is absent from
    the base `WebDriver` and from `FirefoxWebDriver` entirely) - not every browser/driver combination
    exposes the browser console log, so this is best-effort only, same as the C# sibling's own
    try/catch around the equivalent call."""
    get_log = getattr(driver, "get_log", None)
    if get_log is None:
        return

    try:
        entries = get_log("browser")
    except WebDriverException:
        return

    if not entries:
        return

    lines = [f"[{entry.get('level')}] {entry.get('timestamp')} {entry.get('message')}" for entry in entries]
    allure.attach("\n".join(lines), name="browser-console-log", attachment_type=allure.attachment_type.TEXT)
