package com.saucedemo.uitests.steps;

import com.saucedemo.uitests.hooks.ScenarioContext;
import com.saucedemo.uitests.pages.CartPage;
import com.saucedemo.uitests.pages.InventoryPage;
import com.saucedemo.uitests.support.Polling;
import io.cucumber.datatable.DataTable;
import io.cucumber.java.en.Then;
import io.cucumber.java.en.When;
import java.util.List;

public class CartSteps {

    private final ScenarioContext context;

    public CartSteps(ScenarioContext context) {
        this.context = context;
    }

    @Then("the cart badge should show {int}")
    public void theCartBadgeShouldShow(int expectedCount) {
        // Polled, not read once: the badge re-renders asynchronously after a click.
        Polling.assertEventually(
                () -> new InventoryPage(context.driver()).cartCount(), expectedCount, "cart badge count");
    }

    @When("I remove {string} from the cart")
    public void iRemoveFromTheCart(String productName) {
        new InventoryPage(context.driver()).removeFromCart(productName);
    }

    @When("I start checkout")
    public void iStartCheckout() {
        new CartPage(context.driver()).startCheckout();
    }

    @Then("the cart should list the following products:")
    public void theCartShouldListTheFollowingProducts(DataTable table) {
        List<String> expected = table.asMaps().stream()
                .map(row -> row.get("ProductName"))
                .sorted()
                .toList();

        Polling.assertEventually(
                () -> new CartPage(context.driver()).listItemNames().stream().sorted().toList(),
                expected,
                "cart item names");
    }
}
