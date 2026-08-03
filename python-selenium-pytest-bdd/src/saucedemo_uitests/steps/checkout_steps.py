import re
from dataclasses import replace
from decimal import Decimal

from pytest_bdd import parsers, then, when
from selenium.webdriver.remote.webdriver import WebDriver

from saucedemo_uitests.pages.checkout_complete_page import CheckoutCompletePage
from saucedemo_uitests.pages.checkout_information_page import CheckoutInformationPage
from saucedemo_uitests.pages.checkout_overview_page import CheckoutOverviewPage
from saucedemo_uitests.support.polling import assert_eventually
from saucedemo_uitests.testdata.checkout_data_factory import create_checkout_info

_TOTAL_LABEL_PATTERN = re.compile(r"^Total: \$(\d+\.\d{2})$")


@when("I fill in my checkout information")
def fill_in_my_checkout_information(driver: WebDriver) -> None:
    CheckoutInformationPage(driver).fill_and_continue(create_checkout_info())


@when("I fill in my checkout information without a postal code")
def fill_in_my_checkout_information_without_a_postal_code(driver: WebDriver) -> None:
    info = replace(create_checkout_info(), postal_code="")
    CheckoutInformationPage(driver).submit_without_postal_code(info)


@then(parsers.parse("the overview should list {expected_count:d} items"))
def the_overview_should_list_items(driver: WebDriver, expected_count: int) -> None:
    assert_eventually(
        lambda: len(CheckoutOverviewPage(driver).list_item_names()),
        expected_count,
        "checkout overview item count",
    )


@then("the order total should be a positive dollar amount")
def the_order_total_should_be_a_positive_dollar_amount(driver: WebDriver) -> None:
    total_label = CheckoutOverviewPage(driver).get_total_label()
    match = _TOTAL_LABEL_PATTERN.match(total_label)

    assert match is not None, f"'{total_label}' did not match the expected 'Total: $<amount>' format."
    assert Decimal(match.group(1)) > 0


@when("I finish the order")
def finish_the_order(driver: WebDriver) -> None:
    CheckoutOverviewPage(driver).finish()


@then(parsers.parse('I should see the order confirmation "{expected_message}"'))
def should_see_the_order_confirmation(driver: WebDriver, expected_message: str) -> None:
    assert CheckoutCompletePage(driver).get_confirmation_message() == expected_message


@then(parsers.parse('I should see the checkout error "{expected_message}"'))
def should_see_the_checkout_error(driver: WebDriver, expected_message: str) -> None:
    assert CheckoutInformationPage(driver).get_error_message() == expected_message
