package com.saucedemo.uitests.pages;

import com.saucedemo.uitests.testdata.CheckoutInfo;
import org.openqa.selenium.By;
import org.openqa.selenium.WebDriver;

public final class CheckoutInformationPage extends BasePage {

    private static final By FIRST_NAME_INPUT = By.cssSelector("[data-test='firstName']");
    private static final By LAST_NAME_INPUT = By.cssSelector("[data-test='lastName']");
    private static final By POSTAL_CODE_INPUT = By.cssSelector("[data-test='postalCode']");
    private static final By CONTINUE_BUTTON = By.cssSelector("[data-test='continue']");
    private static final By ERROR_BANNER = By.cssSelector("[data-test='error']");

    public CheckoutInformationPage(WebDriver driver) {
        super(driver);
    }

    public CheckoutOverviewPage fillAndContinue(CheckoutInfo info) {
        typeText(FIRST_NAME_INPUT, info.firstName());
        typeText(LAST_NAME_INPUT, info.lastName());
        typeText(POSTAL_CODE_INPUT, info.postalCode());
        click(CONTINUE_BUTTON);
        return new CheckoutOverviewPage(driver);
    }

    /** Leaves the postal code blank and submits, for the "postal code is required" negative scenario. */
    public void submitWithoutPostalCode(CheckoutInfo info) {
        typeText(FIRST_NAME_INPUT, info.firstName());
        typeText(LAST_NAME_INPUT, info.lastName());
        click(CONTINUE_BUTTON);
    }

    public String errorMessage() {
        return textOf(ERROR_BANNER);
    }

    public boolean hasError() {
        return waitAndCheckVisible(ERROR_BANNER);
    }
}
