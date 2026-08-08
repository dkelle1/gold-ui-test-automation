package com.saucedemo.uitests.pages;

import java.time.Duration;
import java.util.List;
import org.openqa.selenium.By;
import org.openqa.selenium.NoSuchElementException;
import org.openqa.selenium.StaleElementReferenceException;
import org.openqa.selenium.TimeoutException;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebElement;

public final class InventoryPage extends BasePage {

    private static final By PRODUCT_NAMES = By.cssSelector("[data-test='inventory-item-name']");
    private static final By CART_BADGE = By.cssSelector("[data-test='shopping-cart-badge']");
    private static final By CART_LINK = By.cssSelector("[data-test='shopping-cart-link']");

    private static final Duration CONFIRM_TIMEOUT = Duration.ofSeconds(3);
    private static final int MAX_TOGGLE_ATTEMPTS = 3;

    public InventoryPage(WebDriver driver) {
        super(driver);
    }

    /**
     * XPath by CSS class plus visible button text. Verified against the live DOM captured by a failing CI
     * run in this gallery's Selenium siblings: each product sits in a {@code <div class="inventory_item">}
     * containing {@code <div data-test="inventory-item-name">}, and the toggle is
     * {@code <button>Add to cart</button>} / {@code <button>Remove</button>}.
     *
     * <p>(saucedemo does also expose {@code data-test="add-to-cart-<slug>"} on those buttons, so a
     * slug-based locator would work too; this one is kept because it needs no slugification.)
     */
    private static By addToCartButtonFor(String productName) {
        return By.xpath("//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='"
                + productName + "']]//button[text()='Add to cart']");
    }

    private static By removeFromCartButtonFor(String productName) {
        return By.xpath("//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='"
                + productName + "']]//button[text()='Remove']");
    }

    public boolean isDisplayed() {
        return waitAndCheckVisible(PRODUCT_NAMES);
    }

    public List<String> listProductNames() {
        return driver.findElements(PRODUCT_NAMES).stream().map(WebElement::getText).toList();
    }

    public void addToCart(String productName) {
        clickAndConfirmToggle(
                addToCartButtonFor(productName), removeFromCartButtonFor(productName), productName, "added to");
    }

    public void removeFromCart(String productName) {
        clickAndConfirmToggle(
                removeFromCartButtonFor(productName), addToCartButtonFor(productName), productName, "removed from");
    }

    /**
     * Establishes an empty cart as a scenario precondition, and (because it waits for the product grid
     * first) doubles as the "login has finished rendering" barrier that {@link LoginPage#submitLogin} does
     * not provide on its own.
     *
     * <p>In practice the removal loop is normally a no-op: saucedemo keeps the cart client-side and every
     * scenario gets a brand-new browser profile (see {@code WebDriverFactory}'s per-session
     * --user-data-dir), so nothing carries over between scenarios or runs. It is kept as an explicit,
     * cheap guard so a scenario asserting exact cart contents can never inherit unexpected state.
     */
    public void clearCart() {
        // listProductNames() is an instant, non-waiting read, so calling it as the very first post-login
        // action risks reading a not-yet-hydrated page and silently clearing nothing. Waiting for the
        // product grid here is what login itself does not guarantee.
        waitForVisible(PRODUCT_NAMES);

        for (String productName : listProductNames()) {
            if (isVisible(removeFromCartButtonFor(productName))) {
                removeFromCart(productName);
            }
        }
    }

    /**
     * The number on the cart badge, or 0 when no badge is rendered - which is how saucedemo shows an empty
     * cart. Reads the element exactly once and never throws: callers poll this repeatedly, so a
     * check-then-read pair would leave a window where the badge disappears between the two calls and the
     * read then blocks for the full explicit wait before throwing - failing the assertion instead of
     * simply reporting 0.
     */
    public int cartCount() {
        try {
            WebElement badge = driver.findElement(CART_BADGE);
            if (!badge.isDisplayed()) {
                return 0;
            }
            try {
                return Integer.parseInt(badge.getText().trim());
            } catch (NumberFormatException e) {
                return 0;
            }
        } catch (NoSuchElementException | StaleElementReferenceException e) {
            return 0;
        }
    }

    public CartPage openCart() {
        click(CART_LINK);
        return new CartPage(driver);
    }

    /**
     * Clicks a toggle button (add-to-cart / remove) and confirms the click actually took effect by waiting
     * for its counterpart button to appear, retrying a few times if it does not.
     *
     * <p>Observed directly in CI in this gallery's Selenium siblings: an "Add to cart" click that reported
     * success (no exception, well under a second) was followed by the cart badge reading 0 for a full
     * 5-second poll - the click reached a real, visible, enabled button, but the app's cart state never
     * changed. A plausible cause is a brief window where the button is visibly interactive before its
     * click handler is fully wired up (a client-side hydration race); re-clicking after confirming the
     * expected effect did not happen is the general-purpose fix regardless of the exact mechanism.
     */
    private void clickAndConfirmToggle(By clickLocator, By counterpartLocator, String productName, String action) {
        int clicks = 0;

        // The button must exist before any of this makes sense. Waiting for it here (rather than relying
        // on the instant isVisible check below) means a not-yet-rendered grid is waited out instead of
        // being mistaken for "the click didn't take effect".
        waitForClickable(clickLocator);

        for (int attempt = 1; attempt <= MAX_TOGGLE_ATTEMPTS; attempt++) {
            // Only re-click while the toggle is still in its "before" state - if an earlier attempt did
            // land (just slower than the confirm timeout), the button will already have flipped, and
            // re-clicking would mean waiting out click()'s full explicit-wait timeout for nothing.
            if (isVisible(clickLocator)) {
                click(clickLocator);
                clicks++;
            }

            if (waitAndCheckVisible(counterpartLocator, CONFIRM_TIMEOUT)) {
                return;
            }
        }

        // Report the real click count. Saying "clicked 3 times" when the button was never in a clickable
        // state (so nothing was clicked at all) points debugging at the app instead of at the page state,
        // which is exactly backwards.
        throw new TimeoutException("'" + productName + "' was never actually " + action + " the cart: over "
                + MAX_TOGGLE_ATTEMPTS + " attempts the toggle button was clicked " + clicks
                + " time(s) and its counterpart (" + counterpartLocator + ") never appeared.");
    }
}
