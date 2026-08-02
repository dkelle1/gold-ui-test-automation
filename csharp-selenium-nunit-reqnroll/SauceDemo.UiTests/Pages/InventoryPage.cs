using OpenQA.Selenium;

namespace SauceDemo.UiTests.Pages;

public sealed class InventoryPage : BasePage
{
    private static readonly By ProductNames = By.CssSelector("[data-test='inventory-item-name']");
    private static readonly By CartBadge = By.CssSelector("[data-test='shopping-cart-badge']");
    private static readonly By CartLink = By.CssSelector("[data-test='shopping-cart-link']");

    public InventoryPage(IWebDriver driver) : base(driver)
    {
    }

    public bool IsDisplayed() => WaitAndCheckVisible(ProductNames);

    public IReadOnlyList<string> ListProductNames() =>
        Driver.FindElements(ProductNames).Select(e => e.Text).ToList();

    public void AddToCart(string productName) =>
        ClickAndConfirmToggle(AddToCartButtonFor(productName), RemoveFromCartButtonFor(productName), productName, "added to");

    public void RemoveFromCart(string productName) =>
        ClickAndConfirmToggle(RemoveFromCartButtonFor(productName), AddToCartButtonFor(productName), productName, "removed from");

    /// <summary>
    /// Removes every product currently in the cart, via the same proven per-product Remove locator as
    /// <see cref="RemoveFromCart"/>. saucedemo persists cart contents against the logged-in account
    /// across sessions rather than resetting per login, and the pooled accounts are reused by many
    /// scenarios across many CI runs - without this, their carts silently accumulate items left behind
    /// by earlier runs, which eventually breaks any assertion on exact cart contents.
    /// </summary>
    public void ClearCart()
    {
        // Login (LoginPage.SubmitLogin) returns as soon as the login click is dispatched - it does not
        // wait for the resulting inventory page to finish rendering. ListProductNames() is an instant,
        // non-waiting read (by design - see BasePage.IsVisible's doc comment), so calling it as the very
        // first post-login action risks reading an empty/not-yet-hydrated page and silently clearing
        // nothing. Waiting for the product grid here first is what login itself doesn't guarantee.
        WaitForVisible(ProductNames);

        foreach (var productName in ListProductNames())
        {
            if (IsVisible(RemoveFromCartButtonFor(productName)))
            {
                RemoveFromCart(productName);
            }
        }
    }

    public int GetCartCount() => IsVisible(CartBadge) ? int.Parse(TextOf(CartBadge)) : 0;

    public CartPage OpenCart()
    {
        Click(CartLink);
        return new CartPage(Driver);
    }

    /// <summary>
    /// Clicks a toggle button (add-to-cart/remove) and confirms the click actually took effect by
    /// waiting for its counterpart button to appear, retrying the click a few times if not. Observed
    /// directly in CI: an "Add to cart" click that reported success (no exception, well under a second)
    /// was followed by the cart badge reading 0 for a full 5-second poll - the click reached a real,
    /// visible, enabled button, but the app's cart state never actually changed. A plausible cause is a
    /// brief window where the button is visibly interactive before its click handler is fully wired up
    /// (a client-side hydration race); retrying the click after confirming the expected effect didn't
    /// happen is the general-purpose fix regardless of the exact mechanism.
    /// </summary>
    private void ClickAndConfirmToggle(By clickLocator, By counterpartLocator, string productName, string action)
    {
        const int maxAttempts = 3;
        var confirmTimeout = TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Only re-click if the toggle button is still in its "before" state - if an earlier attempt
            // actually did land (just slower than confirmTimeout), the button will already have flipped,
            // and re-clicking it would mean waiting out Click()'s full explicit-wait timeout for nothing.
            if (IsVisible(clickLocator))
            {
                Click(clickLocator);
            }

            if (WaitAndCheckVisible(counterpartLocator, confirmTimeout))
            {
                return;
            }
        }

        throw new WebDriverTimeoutException(
            $"Clicked to toggle '{productName}' {maxAttempts} times, but it was never actually {action} the cart.");
    }

    // XPath by CSS class + visible button text, not a data-test locator: a live-CI run proved a
    // guessed data-test="add-to-cart-<slug>" convention wrong for this site (element found and
    // clicked in <1s, but the resulting page state showed it hadn't targeted the right control).
    // This exact XPath shape is the empirically-verified-working form (18/18 in CI run #7).
    private static By AddToCartButtonFor(string productName) => By.XPath(
        $"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Add to cart']");

    private static By RemoveFromCartButtonFor(string productName) => By.XPath(
        $"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Remove']");
}
