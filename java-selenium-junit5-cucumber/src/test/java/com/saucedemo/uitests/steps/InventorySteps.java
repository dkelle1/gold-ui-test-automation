package com.saucedemo.uitests.steps;

import com.saucedemo.uitests.hooks.ScenarioContext;
import com.saucedemo.uitests.pages.InventoryPage;
import io.cucumber.datatable.DataTable;
import io.cucumber.java.en.Given;
import io.cucumber.java.en.When;
import java.util.Map;

public class InventorySteps {

    private final ScenarioContext context;

    public InventorySteps(ScenarioContext context) {
        this.context = context;
    }

    @When("I add {string} to the cart")
    public void iAddToTheCart(String productName) {
        new InventoryPage(context.driver()).addToCart(productName);
    }

    @When("I add the following products to the cart:")
    public void iAddTheFollowingProductsToTheCart(DataTable table) {
        InventoryPage inventoryPage = new InventoryPage(context.driver());

        for (Map<String, String> row : table.asMaps()) {
            inventoryPage.addToCart(row.get("ProductName"));
        }
    }

    @When("I go to the cart")
    public void iGoToTheCart() {
        new InventoryPage(context.driver()).openCart();
    }

    @Given("my cart is empty")
    public void myCartIsEmpty() {
        new InventoryPage(context.driver()).clearCart();
    }
}
