using System.Globalization;
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
    /// Establishes an empty cart as a scenario precondition, and (because it waits for the product
    /// grid first) doubles as the "login has finished rendering" barrier that
    /// <see cref="LoginPage.SubmitLogin"/> does not provide on its own.
    ///
    /// In practice the removal loop is normally a no-op: saucedemo keeps the cart client-side and
    /// every scenario gets a brand-new browser profile (see Drivers.WebDriverFactory's per-session
    /// --user-data-dir), so nothing carries over between scenarios or runs. It is kept as an explicit,
    /// cheap guard so a scenario asserting exact cart contents can never inherit unexpected state.
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

    /// <summary>
    /// The number on the cart badge, or 0 when no badge is rendered - which is how saucedemo shows an
    /// empty cart. Reads the element exactly once and never throws: callers poll this repeatedly (see
    /// CartSteps' `.After(...)` assertions), so a check-then-read pair would leave a window where the
    /// badge disappears between the two calls and the read then blocks for the full explicit wait
    /// before throwing - failing the assertion instead of simply reporting 0.
    /// </summary>
    public int GetCartCount()
    {
        try
        {
            var badge = Driver.FindElement(CartBadge);

            return badge.Displayed
                && int.TryParse(badge.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }
        catch (NoSuchElementException)
        {
            return 0;
        }
        catch (StaleElementReferenceException)
        {
            return 0;
        }
    }

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
        var clicks = 0;

        // The button must exist before any of this makes sense. Waiting for it here (rather than
        // relying on the instant IsVisible check below) means a not-yet-rendered grid is waited out
        // instead of being mistaken for "the click didn't take effect".
        WaitForClickable(clickLocator);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Only re-click if the toggle button is still in its "before" state - if an earlier attempt
            // actually did land (just slower than confirmTimeout), the button will already have flipped,
            // and re-clicking it would mean waiting out Click()'s full explicit-wait timeout for nothing.
            if (IsVisible(clickLocator))
            {
                Click(clickLocator);
                clicks++;
            }

            if (WaitAndCheckVisible(counterpartLocator, confirmTimeout))
            {
                return;
            }
        }

        // Report the real click count. Saying "clicked 3 times" when the button was never in a
        // clickable state (so nothing was clicked at all) points debugging at the app instead of at
        // the page state, which is exactly backwards.
        throw new WebDriverTimeoutException(
            $"'{productName}' was never actually {action} the cart: over {maxAttempts} attempts the toggle " +
            $"button was clicked {clicks} time(s) and its counterpart ({counterpartLocator}) never appeared.");
    }

    // XPath by CSS class + visible button text. Verified against the live DOM captured by a failing
    // CI run: each product sits in <div class="inventory_item"> containing
    // <div data-test="inventory-item-name">, and the toggle is <button>Add to cart</button> /
    // <button>Remove</button>. Both XPaths resolve to exactly one element there.
    //
    // (saucedemo does *also* expose data-test="add-to-cart-<slug>" / "remove-<slug>" on those buttons,
    // so a slug-based locator would work too - an earlier note here claiming CI had disproved that
    // convention was wrong. Either form is fine; this one is kept because it needs no slugification.)
    private static By AddToCartButtonFor(string productName) => By.XPath(
        $"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Add to cart']");

    private static By RemoveFromCartButtonFor(string productName) => By.XPath(
        $"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Remove']");
}
