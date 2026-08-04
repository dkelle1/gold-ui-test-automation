from selenium.webdriver.common.by import By

from saucedemo_uitests.pages.base_page import BasePage, Locator
from saucedemo_uitests.pages.checkout_overview_page import CheckoutOverviewPage
from saucedemo_uitests.testdata.checkout_info import CheckoutInfo

_FIRST_NAME_INPUT: Locator = (By.CSS_SELECTOR, "[data-test='firstName']")
_LAST_NAME_INPUT: Locator = (By.CSS_SELECTOR, "[data-test='lastName']")
_POSTAL_CODE_INPUT: Locator = (By.CSS_SELECTOR, "[data-test='postalCode']")
_CONTINUE_BUTTON: Locator = (By.CSS_SELECTOR, "[data-test='continue']")
_ERROR_BANNER: Locator = (By.CSS_SELECTOR, "[data-test='error']")


class CheckoutInformationPage(BasePage):
    def fill_and_continue(self, info: CheckoutInfo) -> CheckoutOverviewPage:
        self.type_text(_FIRST_NAME_INPUT, info.first_name)
        self.type_text(_LAST_NAME_INPUT, info.last_name)
        self.type_text(_POSTAL_CODE_INPUT, info.postal_code)
        self.click(_CONTINUE_BUTTON)
        return CheckoutOverviewPage(self.driver)

    def submit_without_postal_code(self, info: CheckoutInfo) -> None:
        """Leaves the postal code blank and submits, for the "postal code is required" negative
        scenario."""
        self.type_text(_FIRST_NAME_INPUT, info.first_name)
        self.type_text(_LAST_NAME_INPUT, info.last_name)
        self.click(_CONTINUE_BUTTON)

    def get_error_message(self) -> str:
        return self.text_of(_ERROR_BANNER)

    def has_error(self) -> bool:
        return self.wait_and_check_visible(_ERROR_BANNER)
