from pytest_bdd import given, parsers, when
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.pages.inventory_page import InventoryPage


@when(parsers.parse('I add "{product_name}" to the cart'))
def add_product_to_the_cart(driver: WebDriver, product_name: str) -> None:
    InventoryPage(driver).add_to_cart(product_name)


@when("I add the following products to the cart:")
def add_the_following_products_to_the_cart(driver: WebDriver, datatable: list[list[str]]) -> None:
    header, *rows = datatable
    name_index = header.index("ProductName")

    inventory_page = InventoryPage(driver)
    for row in rows:
        inventory_page.add_to_cart(row[name_index])


@when("I go to the cart")
def go_to_the_cart(driver: WebDriver) -> None:
    InventoryPage(driver).open_cart()


@given("my cart is empty")
def my_cart_is_empty(driver: WebDriver) -> None:
    InventoryPage(driver).clear_cart()
