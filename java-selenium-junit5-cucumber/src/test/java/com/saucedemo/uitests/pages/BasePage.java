package com.saucedemo.uitests.pages;

import com.saucedemo.uitests.config.ConfigurationLoader;
import java.time.Duration;
import org.openqa.selenium.By;
import org.openqa.selenium.NoSuchElementException;
import org.openqa.selenium.StaleElementReferenceException;
import org.openqa.selenium.TimeoutException;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;
import org.openqa.selenium.support.ui.WebDriverWait;

/**
 * Base class for saucedemo page objects. Provides waiting click/type/text helpers so no page object or
 * step definition ever needs a bare {@code Thread.sleep} - every interaction waits explicitly for the
 * element to be visible/clickable first, which matters most for {@code performance_glitch_user}'s
 * artificial ~5s delays.
 *
 * <p>Reads the explicit-wait duration from the process-wide {@link ConfigurationLoader} rather than
 * threading it through every page's constructor, matching the sibling frameworks' own base pages.
 */
public abstract class BasePage {

    private static final Duration POLL_INTERVAL = Duration.ofMillis(250);
    private static final Duration CONFIRM_POLL_INTERVAL = Duration.ofMillis(200);
    private static final int MAX_CLICK_ATTEMPTS = 3;

    protected final WebDriver driver;
    protected final Duration explicitWait;
    private final WebDriverWait wait;

    protected BasePage(WebDriver driver) {
        this.driver = driver;
        this.explicitWait = ConfigurationLoader.settings().explicitWait();
        this.wait = new WebDriverWait(driver, explicitWait, POLL_INTERVAL);
        this.wait.ignoring(NoSuchElementException.class).ignoring(StaleElementReferenceException.class);
    }

    protected WebElement waitForVisible(By locator) {
        try {
            return wait.until(d -> {
                WebElement element = d.findElement(locator);
                return element.isDisplayed() ? element : null;
            });
        } catch (TimeoutException e) {
            throw new TimeoutException("Element '" + locator + "' never became visible.", e);
        }
    }

    protected WebElement waitForClickable(By locator) {
        try {
            return wait.until(d -> {
                WebElement element = d.findElement(locator);
                return element.isDisplayed() && element.isEnabled() ? element : null;
            });
        } catch (TimeoutException e) {
            throw new TimeoutException("Element '" + locator + "' never became clickable.", e);
        }
    }

    /**
     * Clicks the element, retrying the whole find-and-click cycle if it goes stale between being located
     * and being clicked (e.g. a re-render triggered by another in-flight interaction) - the old element
     * reference is unusable once stale, so re-finding it is the only fix.
     */
    protected void click(By locator) {
        for (int attempt = 1; attempt <= MAX_CLICK_ATTEMPTS; attempt++) {
            try {
                waitForClickable(locator).click();
                return;
            } catch (StaleElementReferenceException e) {
                if (attempt == MAX_CLICK_ATTEMPTS) {
                    throw e;
                }
            }
        }
    }

    protected void typeText(By locator, String text) {
        WebElement element = waitForVisible(locator);
        element.clear();
        element.sendKeys(text);
    }

    protected String textOf(By locator) {
        return waitForVisible(locator).getText();
    }

    /**
     * Instant, non-waiting presence check - correct where absence is a normal outcome (e.g. an empty cart
     * badge), not a race to wait out.
     */
    protected boolean isVisible(By locator) {
        try {
            return driver.findElement(locator).isDisplayed();
        } catch (NoSuchElementException e) {
            return false;
        } catch (StaleElementReferenceException e) {
            // Re-rendered between being found and being queried. Callers use this to ask "is it there
            // right now?", and a mid-flight re-render is a legitimate "not right now" - letting it
            // escape would turn a routine race into a scenario failure.
            return false;
        }
    }

    /** Polls for the element's full explicit wait; see {@link #waitAndCheckVisible(By, Duration)}. */
    protected boolean waitAndCheckVisible(By locator) {
        return waitAndCheckVisible(locator, explicitWait);
    }

    /**
     * Polls up to the given timeout for the element to appear, returning false (never throwing) if it
     * does not - for positive assertions ("an error banner should show up") where a brief render delay is
     * expected but permanent absence is a real, reportable outcome rather than a bug in the wait.
     *
     * <p>Takes a caller-supplied timeout so a short, bounded check can sit inside a retry loop, where
     * waiting the full explicit wait on every attempt would make the retry pointless.
     */
    protected boolean waitAndCheckVisible(By locator, Duration timeout) {
        WebDriverWait boundedWait = new WebDriverWait(driver, timeout, CONFIRM_POLL_INTERVAL);
        try {
            return Boolean.TRUE.equals(boundedWait.until(d -> isVisible(locator) ? Boolean.TRUE : null));
        } catch (TimeoutException e) {
            return false;
        }
    }
}
