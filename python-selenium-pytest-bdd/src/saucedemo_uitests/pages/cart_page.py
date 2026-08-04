from selenium.webdriver.common.by import By

from saucedemo_uitests.pages.base_page import BasePage, Locator
from saucedemo_uitests.pages.checkout_information_page import CheckoutInformationPage

_CART_ITEM_NAMES: Locator = (By.CSS_SELECTOR, "[data-test='inventory-item-name']")
_CHECKOUT_BUTTON: Locator = (By.CSS_SELECTOR, "[data-test='checkout']")


class CartPage(BasePage):
    def list_item_names(self) -> list[str]:
        return [element.text for element in self.driver.find_elements(*_CART_ITEM_NAMES)]

    def start_checkout(self) -> CheckoutInformationPage:
        self.click(_CHECKOUT_BUTTON)
        return CheckoutInformationPage(self.driver)
