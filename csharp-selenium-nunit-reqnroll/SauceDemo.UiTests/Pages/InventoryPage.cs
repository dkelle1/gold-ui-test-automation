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

    public bool IsDisplayed() => IsVisible(ProductNames);

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

    private static By AddToCartButtonFor(string productName) => By.XPath(
        $"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Add to cart']");

    private static By RemoveFromCartButtonFor(string productName) => By.XPath(
        $"//div[@class='inventory_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Remove']");
}
