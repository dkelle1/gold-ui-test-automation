from pytest_bdd import parsers, then, when
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.pages.cart_page import CartPage
from saucedemo_uitests.pages.inventory_page import InventoryPage
from saucedemo_uitests.support.polling import assert_eventually


@then(parsers.parse("the cart badge should show {expected_count:d}"))
def the_cart_badge_should_show(driver: WebDriver, expected_count: int) -> None:
    assert_eventually(lambda: InventoryPage(driver).get_cart_count(), expected_count, "cart badge count")


@when(parsers.parse('I remove "{product_name}" from the cart'))
def remove_product_from_the_cart(driver: WebDriver, product_name: str) -> None:
    InventoryPage(driver).remove_from_cart(product_name)


@when("I start checkout")
def start_checkout(driver: WebDriver) -> None:
    CartPage(driver).start_checkout()


@then("the cart should list the following products:")
def the_cart_should_list_the_following_products(driver: WebDriver, datatable: list[list[str]]) -> None:
    header, *rows = datatable
    name_index = header.index("ProductName")
    expected = sorted(row[name_index] for row in rows)

    assert_eventually(lambda: sorted(CartPage(driver).list_item_names()), expected, "cart item names")
