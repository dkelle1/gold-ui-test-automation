from selenium.webdriver.common.by import By

from saucedemo_uitests.pages.base_page import BasePage, Locator
from saucedemo_uitests.pages.checkout_complete_page import CheckoutCompletePage

_ITEM_NAMES: Locator = (By.CSS_SELECTOR, "[data-test='inventory-item-name']")
_TOTAL_LABEL: Locator = (By.CSS_SELECTOR, "[data-test='total-label']")
_FINISH_BUTTON: Locator = (By.CSS_SELECTOR, "[data-test='finish']")


class CheckoutOverviewPage(BasePage):
    def list_item_names(self) -> list[str]:
        return [element.text for element in self.driver.find_elements(*_ITEM_NAMES)]

    def get_total_label(self) -> str:
        return self.text_of(_TOTAL_LABEL)

    def finish(self) -> CheckoutCompletePage:
        self.click(_FINISH_BUTTON)
        return CheckoutCompletePage(self.driver)
