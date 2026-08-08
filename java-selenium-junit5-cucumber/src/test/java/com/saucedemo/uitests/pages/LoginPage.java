package com.saucedemo.uitests.pages;

import org.openqa.selenium.By;
import org.openqa.selenium.WebDriver;

public final class LoginPage extends BasePage {

    private static final By USERNAME_INPUT = By.cssSelector("[data-test='username']");
    private static final By PASSWORD_INPUT = By.cssSelector("[data-test='password']");
    private static final By LOGIN_BUTTON = By.cssSelector("[data-test='login-button']");
    private static final By ERROR_BANNER = By.cssSelector("[data-test='error']");

    public LoginPage(WebDriver driver) {
        super(driver);
    }

    /**
     * Submits the login form without asserting the outcome - the caller decides whether success or a
     * specific error is expected. Note this returns as soon as the click is dispatched; it does not wait
     * for the inventory page to finish rendering (see {@link InventoryPage#clearCart()}).
     */
    public void submitLogin(String username, String password) {
        typeText(USERNAME_INPUT, username);
        typeText(PASSWORD_INPUT, password);
        click(LOGIN_BUTTON);
    }

    public String errorMessage() {
        return textOf(ERROR_BANNER);
    }

    public boolean hasError() {
        return waitAndCheckVisible(ERROR_BANNER);
    }
}
