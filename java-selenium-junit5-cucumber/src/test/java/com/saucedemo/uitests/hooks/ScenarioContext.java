package com.saucedemo.uitests.hooks;

import com.saucedemo.uitests.drivers.BrowserSession;
import com.saucedemo.uitests.users.UserAccount;
import com.saucedemo.uitests.users.UserLease;
import org.openqa.selenium.WebDriver;

/**
 * Per-scenario state shared between {@link ScenarioHooks} and the step-definition classes.
 *
 * <p>Cucumber's PicoContainer module builds a fresh object graph for every scenario and hands the same
 * instance to every class in that scenario that asks for it, which is what makes this safe with three
 * scenarios running concurrently: there is no static or thread-local state here, just one plain object
 * per scenario. It is the Cucumber-JVM counterpart of the BoDi scenario container the C# siblings use.
 */
public class ScenarioContext {

    private BrowserSession browserSession;
    private UserLease userLease;

    public void setBrowserSession(BrowserSession browserSession) {
        this.browserSession = browserSession;
    }

    public void setUserLease(UserLease userLease) {
        this.userLease = userLease;
    }

    public boolean hasBrowserSession() {
        return browserSession != null;
    }

    public BrowserSession browserSession() {
        return browserSession;
    }

    public UserLease userLease() {
        return userLease;
    }

    public WebDriver driver() {
        if (browserSession == null) {
            throw new IllegalStateException(
                    "No browser session for this scenario - ScenarioHooks should have created one.");
        }
        return browserSession.driver();
    }

    public UserAccount user() {
        if (userLease == null) {
            throw new IllegalStateException(
                    "No user assigned to this scenario - ScenarioHooks should have leased one.");
        }
        return userLease.account();
    }
}
