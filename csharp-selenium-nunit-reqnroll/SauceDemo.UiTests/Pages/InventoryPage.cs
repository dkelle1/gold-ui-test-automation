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
