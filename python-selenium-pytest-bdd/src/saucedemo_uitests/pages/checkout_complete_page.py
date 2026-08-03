from selenium.webdriver.common.by import By

from saucedemo_uitests.pages.base_page import BasePage, Locator

_COMPLETE_HEADER: Locator = (By.CSS_SELECTOR, "[data-test='complete-header']")


class CheckoutCompletePage(BasePage):
    def get_confirmation_message(self) -> str:
        return self.text_of(_COMPLETE_HEADER)
