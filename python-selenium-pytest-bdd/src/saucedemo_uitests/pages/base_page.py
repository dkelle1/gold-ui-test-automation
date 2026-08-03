"""Base class for saucedemo page objects. Provides waiting click/type/text helpers so no step
definition or page object ever needs `time.sleep` - every interaction waits explicitly for the element
to be visible/clickable first, which matters most for `performance_glitch_user`'s artificial ~5s delays.

Reads the explicit-wait duration from the process-wide `get_settings()` rather than threading it through
every page's constructor, matching the C# sibling's `BasePage` (which resolves the same setting from its
own process-wide `ConfigurationLoader.Settings`).
"""

from typing import Literal

from selenium.common.exceptions import (
    NoSuchElementException,
    StaleElementReferenceException,
    TimeoutException,
)
from selenium.webdriver.remote.webdriver import WebDriver
from selenium.webdriver.remote.webelement import WebElement
from selenium.webdriver.support.wait import WebDriverWait

from saucedemo_uitests.config.settings import get_settings

Locator = tuple[str, str]

_POLL_FREQUENCY_SECONDS = 0.25
_CONFIRM_POLL_FREQUENCY_SECONDS = 0.2


def _visible_or_false(element: WebElement) -> Literal[False] | WebElement:
    # `WebDriverWait.until` treats `None` exactly like `False` at runtime (its loop is a plain `if
    # value:` truthiness check - confirmed directly from the installed selenium source) but its type
    # signature only declares `Literal[False] | T`, so returning `False` here (never `None`) is what lets
    # mypy infer `T = WebElement` and `until(...)` come back as `WebElement`, not `WebElement | None`.
    return element if element.is_displayed() else False


def _clickable_or_false(element: WebElement) -> Literal[False] | WebElement:
    return element if element.is_displayed() and element.is_enabled() else False


class BasePage:
    def __init__(self, driver: WebDriver) -> None:
        self.driver = driver
        self._explicit_wait_seconds = get_settings().explicit_wait_seconds
        self._wait = WebDriverWait(
            driver,
            self._explicit_wait_seconds,
            poll_frequency=_POLL_FREQUENCY_SECONDS,
            ignored_exceptions=(NoSuchElementException, StaleElementReferenceException),
        )

    def wait_for_visible(self, locator: Locator) -> WebElement:
        try:
            return self._wait.until(lambda d: _visible_or_false(d.find_element(*locator)))
        except TimeoutException:
            raise TimeoutException(f"Element '{locator}' never became visible.") from None

    def wait_for_clickable(self, locator: Locator) -> WebElement:
        try:
            return self._wait.until(lambda d: _clickable_or_false(d.find_element(*locator)))
        except TimeoutException:
            raise TimeoutException(f"Element '{locator}' never became clickable.") from None

    def click(self, locator: Locator) -> None:
        """Clicks the element, retrying the whole find-and-click cycle if the element goes stale between
        being located and being clicked (e.g. a re-render triggered by another in-flight interaction) -
        the old element reference is unusable once stale, so re-finding it is the only fix."""
        max_attempts = 3

        for attempt in range(1, max_attempts + 1):
            try:
                self.wait_for_clickable(locator).click()
                return
            except StaleElementReferenceException:
                if attempt >= max_attempts:
                    raise

    def type_text(self, locator: Locator, text: str) -> None:
        element = self.wait_for_visible(locator)
        element.clear()
        element.send_keys(text)

    def text_of(self, locator: Locator) -> str:
        return self.wait_for_visible(locator).text

    def is_visible(self, locator: Locator) -> bool:
        """Instant, non-waiting presence check - correct where absence is a normal outcome (e.g. an
        empty cart badge), not a race to wait out."""
        try:
            return self.driver.find_element(*locator).is_displayed()
        except NoSuchElementException:
            return False
        except StaleElementReferenceException:
            # The element was re-rendered between being found and being queried. Callers use this to
            # ask "is it there right now?", and a mid-flight re-render is a legitimate "not right now" -
            # letting it escape would turn a routine race into a scenario failure.
            return False

    def wait_and_check_visible(self, locator: Locator, timeout_seconds: float | None = None) -> bool:
        """Polls up to the given timeout (the page's full explicit wait by default) for the element to
        appear, returning False (never raising) if it doesn't - for positive assertions ("an error banner
        should show up") where a brief render delay is expected but permanent absence is a real,
        reportable outcome rather than a bug in the wait.

        Accepts a caller-supplied timeout for a short, bounded check inside a retry loop, where waiting
        the full explicit wait on every attempt would make the retry pointless.
        """
        wait = WebDriverWait(
            self.driver,
            timeout_seconds if timeout_seconds is not None else self._explicit_wait_seconds,
            poll_frequency=_CONFIRM_POLL_FREQUENCY_SECONDS,
        )

        try:
            return wait.until(lambda d: _is_visible_no_raise(d, locator))
        except TimeoutException:
            return False


def _is_visible_no_raise(driver: WebDriver, locator: Locator) -> bool:
    try:
        return driver.find_element(*locator).is_displayed()
    except NoSuchElementException:
        return False
    except StaleElementReferenceException:
        # Re-rendered mid-poll; this method must never raise, so keep polling instead.
        return False
