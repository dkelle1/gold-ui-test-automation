using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SauceDemo.UiTests.Pages;

public sealed class InventoryPage : BasePage
{
    private static readonly By ProductNames = By.CssSelector("[data-test='inventory-item-name']");
    private static readonly By CartBadge = By.CssSelector("[data-test='shopping-cart-badge']");
    private static readonly By CartLink = By.CssSelector("[data-test='shopping-cart-link']");
    private static readonly By SortDropdown = By.CssSelector("[data-test='product_sort_container']");
    private static readonly By BurgerMenuButton = By.Id("react-burger-menu-btn");
    private static readonly By LogoutLink = By.CssSelector("[data-test='logout-sidebar-link']");

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

    /// <summary>Selects a sort option by its saucedemo &lt;option value&gt; - "az", "za", "lohi", or "hilo".</summary>
    public void SortBy(string optionValue)
    {
        var select = new SelectElement(WaitForVisible(SortDropdown));
        select.SelectByValue(optionValue);
    }

    public LoginPage Logout()
    {
        Click(BurgerMenuButton);
        Click(LogoutLink);
        return new LoginPage(Driver);
    }

    private static By AddToCartButtonFor(string productName) => By.CssSelector($"[data-test='add-to-cart-{ProductSlug(productName)}']");

    private static By RemoveFromCartButtonFor(string productName) => By.CssSelector($"[data-test='remove-{ProductSlug(productName)}']");
}
