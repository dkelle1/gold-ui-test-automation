using System.Globalization;
using Microsoft.Playwright;

namespace SauceDemo.UiTests.Pages;

public sealed class InventoryPage : BasePage
{
    // Matches all six product-name cells, not one - so every *waiting* use of it has to go through
    // BasePage's ...AnyVisibleAsync helpers, or Playwright's strict mode rejects it outright. Reading
    // it via AllTextContentsAsync (below) is fine: that method is multi-element by design.
    private const string ProductNames = "[data-test='inventory-item-name']";
    private const string CartBadge = "[data-test='shopping-cart-badge']";
    private const string CartLink = "[data-test='shopping-cart-link']";

    public InventoryPage(IPage page) : base(page)
    {
    }

    public Task<bool> IsDisplayedAsync() => WaitAndCheckAnyVisibleAsync(ProductNames);

    public async Task<IReadOnlyList<string>> ListProductNamesAsync() =>
        await Page.Locator(ProductNames).AllTextContentsAsync();

    public Task AddToCartAsync(string productName) =>
        ClickAndConfirmToggleAsync(AddToCartButtonFor(productName), RemoveFromCartButtonFor(productName), productName, "added to");

    public Task RemoveFromCartAsync(string productName) =>
        ClickAndConfirmToggleAsync(RemoveFromCartButtonFor(productName), AddToCartButtonFor(productName), productName, "removed from");

    /// <summary>
    /// Establishes an empty cart as a scenario precondition, and (because it waits for the product
    /// grid first) doubles as the "login has finished rendering" barrier that
    /// <see cref="LoginPage.SubmitLoginAsync"/> does not provide on its own.
    ///
    /// In practice the removal loop is normally a no-op: saucedemo keeps the cart client-side, and
    /// every scenario gets a brand-new, fully isolated <see cref="Microsoft.Playwright.IBrowserContext"/>
    /// (see Drivers.PlaywrightFactory), so nothing carries over between scenarios or runs. It is kept as
    /// an explicit, cheap guard so a scenario asserting exact cart contents can never inherit unexpected
    /// state.
    /// </summary>
    public async Task ClearCartAsync()
    {
        // Login (LoginPage.SubmitLoginAsync) returns as soon as the login click is dispatched - it does
        // not wait for the resulting inventory page to finish rendering. Waiting for the product grid
        // here first is what login itself doesn't guarantee.
        await WaitForAnyVisibleAsync(ProductNames);

        foreach (var productName in await ListProductNamesAsync())
        {
            if (await IsVisibleAsync(RemoveFromCartButtonFor(productName)))
            {
                await RemoveFromCartAsync(productName);
            }
        }
    }

    /// <summary>
    /// The number on the cart badge, or 0 when no badge is rendered - which is how saucedemo shows an
    /// empty cart. A check-then-read pair would leave a window where the badge disappears between the
    /// two calls and the read then blocks for the full context timeout before throwing - failing the
    /// assertion instead of simply reporting 0 - so this instead makes one read with a short timeout of
    /// its own and treats "never showed up that quickly" as absence. Callers poll this repeatedly (see
    /// CartSteps' `.After(...)` assertions), so it must stay cheap on the common "badge isn't there" path.
    /// </summary>
    public async Task<int> GetCartCountAsync()
    {
        try
        {
            var text = await Page.Locator(CartBadge).TextContentAsync(new LocatorTextContentOptions { Timeout = 500 });

            return text is not null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }
        catch (TimeoutException)
        {
            // "No badge within 500ms" is the empty cart, not an error. It has to be TimeoutException and
            // not PlaywrightException: Playwright reuses the BCL's System.TimeoutException, which does
            // not derive from PlaywrightException - see the note above BasePage.WaitForVisibleAsync.
            return 0;
        }
    }

    public async Task<CartPage> OpenCartAsync()
    {
        await ClickAsync(CartLink);
        return new CartPage(Page);
    }

    /// <summary>
    /// Clicks a toggle button (add-to-cart/remove) and confirms the click actually took effect by
    /// waiting for its counterpart button to appear, retrying a few times if not. The Selenium sibling
    /// needed this because a WebDriver click that reported success (no exception) was sometimes followed
    /// by the cart state never actually changing - plausibly a brief window where the button looked
    /// interactive before its handler was fully wired up. Playwright's own auto-waiting (attached,
    /// visible, stable, and actually hit-testable at the click point - not just "Displayed and Enabled"
    /// as WebDriverWait checked) is a meaningfully stronger guard against that specific race. It is kept
    /// here anyway, deliberately, as defense in depth: the Selenium sibling's own investigation of its
    /// remaining CI flakiness concluded the most likely cause was saucedemo.com itself degrading under
    /// repeated automated traffic, not a client-side timing bug - and no client-side library, however
    /// good its waiting, can wait out a server that never changes state. This retry is cheap insurance
    /// against that, not a sign this framework has the same bug the Selenium one did.
    /// </summary>
    private async Task ClickAndConfirmToggleAsync(string clickSelector, string counterpartSelector, string productName, string action)
    {
        const int maxAttempts = 3;
        const float confirmTimeoutMs = 3000;
        var clicks = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // The first attempt always clicks - ClickAsync's own actionability wait is what carries the
            // "wait for a not-yet-rendered grid" job the Selenium sibling needed a separate WaitForClickable
            // call for. Later attempts only re-click if the toggle button is still in its "before" state:
            // if an earlier attempt actually did land (just slower than confirmTimeoutMs), the button will
            // already have flipped, and re-clicking it would mean waiting out the full default timeout for
            // nothing.
            if (attempt == 1 || await IsVisibleAsync(clickSelector))
            {
                await ClickAsync(clickSelector);
                clicks++;
            }

            if (await WaitAndCheckVisibleAsync(counterpartSelector, confirmTimeoutMs))
            {
                return;
            }
        }

        // Report the real click count. Saying "clicked 3 times" when the button was never in a
        // clickable state (so nothing was clicked at all) points debugging at the app instead of at
        // the page state, which is exactly backwards.
        throw new PlaywrightException(
            $"'{productName}' was never actually {action} the cart: over {maxAttempts} attempts the toggle " +
            $"button was clicked {clicks} time(s) and its counterpart ('{counterpartSelector}') never appeared.");
    }

    // XPath by CSS class + visible button text, carried over unchanged from the Selenium sibling's
    // InventoryPage (verified there against the live DOM captured by a failing CI run): each product
    // sits in <div class="inventory_item"> containing <div data-test="inventory-item-name">, and the
    // toggle is <button>Add to cart</button> / <button>Remove</button>. Playwright locators support
    // `xpath=` natively, so this needed no translation.
    //
    // (saucedemo does *also* expose data-test="add-to-cart-<slug>" / "remove-<slug>" on those buttons.
    // That convention is skipped here for the same reason the Selenium sibling skipped it: one catalog
    // product name - "Test.allTheThings() T-Shirt (Red)" - doesn't slugify predictably, and there's no
    // way to verify a guessed slug against the real DOM from this environment.)
    private static string AddToCartButtonFor(string productName) =>
        $"xpath=//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Add to cart']";

    private static string RemoveFromCartButtonFor(string productName) =>
        $"xpath=//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Remove']";
}
