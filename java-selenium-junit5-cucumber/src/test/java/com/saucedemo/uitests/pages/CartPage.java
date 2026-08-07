package com.saucedemo.uitests.pages;

import java.util.List;
import org.openqa.selenium.By;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;

public final class CartPage extends BasePage {

    private static final By CART_ITEM_NAMES = By.cssSelector("[data-test='inventory-item-name']");
    private static final By CHECKOUT_BUTTON = By.cssSelector("[data-test='checkout']");

    public CartPage(WebDriver driver) {
        super(driver);
    }

    public List<String> listItemNames() {
        return driver.findElements(CART_ITEM_NAMES).stream().map(WebElement::getText).toList();
    }

    public CheckoutInformationPage startCheckout() {
        click(CHECKOUT_BUTTON);
        return new CheckoutInformationPage(driver);
    }
}
