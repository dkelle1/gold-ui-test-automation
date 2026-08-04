from pytest_bdd import given, parsers, then, when
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.pages.inventory_page import InventoryPage
from saucedemo_uitests.pages.login_page import LoginPage
from saucedemo_uitests.testdata.invalid_credentials_factory import create_invalid_credentials
from saucedemo_uitests.testdata.product_catalog import ALL as ALL_PRODUCTS
from saucedemo_uitests.users.user_account import UserAccount

_GENERATED_MARKER = "<generated>"


@given("I log in with my assigned user")
@when("I log in with my assigned user")
def log_in_with_my_assigned_user(driver: WebDriver, user: UserAccount) -> None:
    LoginPage(driver).submit_login(user.username, user.password)


@when(parsers.re(r'I log in with username "(?P<username>.*)" and password "(?P<password>.*)"'))
def log_in_with_username_and_password(driver: WebDriver, username: str, password: str) -> None:
    """The "<generated>" marker lets an Examples row ask for fresh, guaranteed-not-real credentials from
    Faker instead of a fixed literal."""
    if username == _GENERATED_MARKER or password == _GENERATED_MARKER:
        generated_username, generated_password = create_invalid_credentials()
        username = generated_username if username == _GENERATED_MARKER else username
        password = generated_password if password == _GENERATED_MARKER else password

    LoginPage(driver).submit_login(username, password)


@then("I should land on the inventory page")
def should_land_on_the_inventory_page(driver: WebDriver) -> None:
    assert InventoryPage(driver).is_displayed(), "Expected the inventory page to be displayed after login."


@then(parsers.parse("{expected_count:d} products should be listed"))
def products_should_be_listed(driver: WebDriver, expected_count: int) -> None:
    actual = InventoryPage(driver).list_product_names()
    assert len(actual) == expected_count
    assert set(actual) <= set(ALL_PRODUCTS), "Every listed product name should be a known saucedemo product."


@then(parsers.parse('I should see the login error "{expected_message}"'))
def should_see_the_login_error(driver: WebDriver, expected_message: str) -> None:
    assert LoginPage(driver).get_error_message() == expected_message


@then("I should see a login error")
def should_see_a_login_error(driver: WebDriver) -> None:
    assert LoginPage(driver).has_error()
