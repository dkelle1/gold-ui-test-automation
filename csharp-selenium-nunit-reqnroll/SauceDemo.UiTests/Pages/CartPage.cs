using OpenQA.Selenium;

namespace SauceDemo.UiTests.Pages;

public sealed class CartPage : BasePage
{
    private static readonly By CartItemNames = By.CssSelector("[data-test='inventory-item-name']");
    private static readonly By CheckoutButton = By.CssSelector("[data-test='checkout']");

    public CartPage(IWebDriver driver) : base(driver)
    {
    }

    public IReadOnlyList<string> ListItemNames() =>
        Driver.FindElements(CartItemNames).Select(e => e.Text).ToList();

    public void RemoveItem(string productName) => Click(RemoveButtonFor(productName));

    public CheckoutInformationPage StartCheckout()
    {
        Click(CheckoutButton);
        return new CheckoutInformationPage(Driver);
    }

    private static By RemoveButtonFor(string productName) => By.CssSelector($"[data-test='remove-{ProductSlug(productName)}']");
}
