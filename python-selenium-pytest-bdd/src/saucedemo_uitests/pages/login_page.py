from selenium.webdriver.common.by import By

from saucedemo_uitests.pages.base_page import BasePage, Locator

_USERNAME_INPUT: Locator = (By.CSS_SELECTOR, "[data-test='username']")
_PASSWORD_INPUT: Locator = (By.CSS_SELECTOR, "[data-test='password']")
_LOGIN_BUTTON: Locator = (By.CSS_SELECTOR, "[data-test='login-button']")
_ERROR_BANNER: Locator = (By.CSS_SELECTOR, "[data-test='error']")


class LoginPage(BasePage):
    def submit_login(self, username: str, password: str) -> None:
        """Submits the login form without asserting the outcome - the caller decides whether success or
        a specific error is expected."""
        self.type_text(_USERNAME_INPUT, username)
        self.type_text(_PASSWORD_INPUT, password)
        self.click(_LOGIN_BUTTON)

    def get_error_message(self) -> str:
        return self.text_of(_ERROR_BANNER)

    def has_error(self) -> bool:
        return self.wait_and_check_visible(_ERROR_BANNER)
