package com.saucedemo.uitests.pages;

import org.openqa.selenium.By;
import org.openqa.selenium.WebDriver;

public final class CheckoutCompletePage extends BasePage {

    private static final By COMPLETE_HEADER = By.cssSelector("[data-test='complete-header']");

    public CheckoutCompletePage(WebDriver driver) {
        super(driver);
    }

    public String confirmationMessage() {
        return textOf(COMPLETE_HEADER);
    }
}
