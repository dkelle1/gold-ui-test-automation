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

    public void AddToCart(string productName) => Click(AddToCartButtonFor(productName));

    public void RemoveFromCart(string productName) => Click(RemoveFromCartButtonFor(productName));

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
            var removeButton = RemoveFromCartButtonFor(productName);
            if (IsVisible(removeButton))
            {
                Click(removeButton);
            }
        }
    }

    public int GetCartCount() => IsVisible(CartBadge) ? int.Parse(TextOf(CartBadge)) : 0;

    public CartPage OpenCart()
    {
        Click(CartLink);
        return new CartPage(Driver);
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
