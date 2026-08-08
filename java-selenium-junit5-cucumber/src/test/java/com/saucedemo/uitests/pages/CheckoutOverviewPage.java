package com.saucedemo.uitests.pages;

import java.util.List;
import org.openqa.selenium.By;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;

public final class CheckoutOverviewPage extends BasePage {

    private static final By ITEM_NAMES = By.cssSelector("[data-test='inventory-item-name']");
    private static final By TOTAL_LABEL = By.cssSelector("[data-test='total-label']");
    private static final By FINISH_BUTTON = By.cssSelector("[data-test='finish']");

    public CheckoutOverviewPage(WebDriver driver) {
        super(driver);
    }

    public List<String> listItemNames() {
        return driver.findElements(ITEM_NAMES).stream().map(WebElement::getText).toList();
    }

    public String totalLabel() {
        return textOf(TOTAL_LABEL);
    }

    public CheckoutCompletePage finish() {
        click(FINISH_BUTTON);
        return new CheckoutCompletePage(driver);
    }
}
