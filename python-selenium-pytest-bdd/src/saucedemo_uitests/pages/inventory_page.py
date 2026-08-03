from selenium.common.exceptions import (
    NoSuchElementException,
    StaleElementReferenceException,
    TimeoutException,
)
from selenium.webdriver.common.by import By

from saucedemo_uitests.pages.base_page import BasePage, Locator
from saucedemo_uitests.pages.cart_page import CartPage

_PRODUCT_NAMES: Locator = (By.CSS_SELECTOR, "[data-test='inventory-item-name']")
_CART_BADGE: Locator = (By.CSS_SELECTOR, "[data-test='shopping-cart-badge']")
_CART_LINK: Locator = (By.CSS_SELECTOR, "[data-test='shopping-cart-link']")

_CONFIRM_TIMEOUT_SECONDS = 3.0
_MAX_TOGGLE_ATTEMPTS = 3


def _add_to_cart_button_for(product_name: str) -> Locator:
    # XPath by CSS class + visible button text. Verified against the live DOM captured by a failing CI
    # run (see the C# Selenium sibling's InventoryPage): each product sits in
    # <div class="inventory_item"> containing <div data-test="inventory-item-name">, and the toggle is
    # <button>Add to cart</button> / <button>Remove</button>. Both XPaths resolve to exactly one element
    # there.
    #
    # (saucedemo does *also* expose data-test="add-to-cart-<slug>" / "remove-<slug>" on those buttons, so
    # a slug-based locator would work too; this one is kept because it needs no slugification.)
    return (
        By.XPATH,
        f"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and "
        f"text()='{product_name}']]//button[text()='Add to cart']",
    )


def _remove_from_cart_button_for(product_name: str) -> Locator:
    return (
        By.XPATH,
        f"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and "
        f"text()='{product_name}']]//button[text()='Remove']",
    )


class InventoryPage(BasePage):
    def is_displayed(self) -> bool:
        return self.wait_and_check_visible(_PRODUCT_NAMES)

    def list_product_names(self) -> list[str]:
        return [element.text for element in self.driver.find_elements(*_PRODUCT_NAMES)]

    def add_to_cart(self, product_name: str) -> None:
        self._click_and_confirm_toggle(
            _add_to_cart_button_for(product_name),
            _remove_from_cart_button_for(product_name),
            product_name,
            "added to",
        )

    def remove_from_cart(self, product_name: str) -> None:
        self._click_and_confirm_toggle(
            _remove_from_cart_button_for(product_name),
            _add_to_cart_button_for(product_name),
            product_name,
            "removed from",
        )

    def clear_cart(self) -> None:
        """Establishes an empty cart as a scenario precondition, and (because it waits for the product
        grid first) doubles as the "login has finished rendering" barrier that `LoginPage.submit_login`
        does not provide on its own.

        In practice the removal loop is normally a no-op: saucedemo keeps the cart client-side and every
        scenario gets a brand-new browser profile (see `drivers.driver_session`'s per-session
        --user-data-dir), so nothing carries over between scenarios or runs. It is kept as an explicit,
        cheap guard so a scenario asserting exact cart contents can never inherit unexpected state.
        """
        # Login (LoginPage.submit_login) returns as soon as the login click is dispatched - it does not
        # wait for the resulting inventory page to finish rendering. list_product_names() is an instant,
        # non-waiting read (by design - see BasePage.is_visible's docstring), so calling it as the very
        # first post-login action risks reading an empty/not-yet-hydrated page and silently clearing
        # nothing. Waiting for the product grid here first is what login itself doesn't guarantee.
        self.wait_for_visible(_PRODUCT_NAMES)

        for product_name in self.list_product_names():
            if self.is_visible(_remove_from_cart_button_for(product_name)):
                self.remove_from_cart(product_name)

    def get_cart_count(self) -> int:
        """The number on the cart badge, or 0 when no badge is rendered - which is how saucedemo shows an
        empty cart. Reads the element exactly once and never raises: callers poll this repeatedly (see
        `cart_steps`' retry assertions), so a check-then-read pair would leave a window where the badge
        disappears between the two calls and the read then blocks for the full explicit wait before
        raising - failing the assertion instead of simply reporting 0."""
        try:
            badge = self.driver.find_element(*_CART_BADGE)
            if not badge.is_displayed():
                return 0
            try:
                return int(badge.text)
            except ValueError:
                return 0
        except NoSuchElementException:
            return 0
        except StaleElementReferenceException:
            return 0

    def open_cart(self) -> CartPage:
        self.click(_CART_LINK)
        return CartPage(self.driver)

    def _click_and_confirm_toggle(
        self, click_locator: Locator, counterpart_locator: Locator, product_name: str, action: str
    ) -> None:
        """Clicks a toggle button (add-to-cart/remove) and confirms the click actually took effect by
        waiting for its counterpart button to appear, retrying the click a few times if not. Observed
        directly in CI (see the C# Selenium sibling's InventoryPage): an "Add to cart" click that
        reported success (no exception, well under a second) was followed by the cart badge reading 0
        for a full 5-second poll - the click reached a real, visible, enabled button, but the app's cart
        state never actually changed. A plausible cause is a brief window where the button is visibly
        interactive before its click handler is fully wired up (a client-side hydration race); retrying
        the click after confirming the expected effect didn't happen is the general-purpose fix
        regardless of the exact mechanism."""
        clicks = 0

        # The button must exist before any of this makes sense. Waiting for it here (rather than relying
        # on the instant is_visible check below) means a not-yet-rendered grid is waited out instead of
        # being mistaken for "the click didn't take effect".
        self.wait_for_clickable(click_locator)

        for _attempt in range(1, _MAX_TOGGLE_ATTEMPTS + 1):
            # Only re-click if the toggle button is still in its "before" state - if an earlier attempt
            # actually did land (just slower than the confirm timeout), the button will already have
            # flipped, and re-clicking it would mean waiting out click()'s full explicit-wait timeout for
            # nothing.
            if self.is_visible(click_locator):
                self.click(click_locator)
                clicks += 1

            if self.wait_and_check_visible(counterpart_locator, _CONFIRM_TIMEOUT_SECONDS):
                return

        # Report the real click count. Saying "clicked 3 times" when the button was never in a clickable
        # state (so nothing was clicked at all) points debugging at the app instead of at the page state,
        # which is exactly backwards.
        raise TimeoutException(
            f"'{product_name}' was never actually {action} the cart: over {_MAX_TOGGLE_ATTEMPTS} attempts "
            f"the toggle button was clicked {clicks} time(s) and its counterpart ({counterpart_locator}) "
            f"never appeared."
        )
