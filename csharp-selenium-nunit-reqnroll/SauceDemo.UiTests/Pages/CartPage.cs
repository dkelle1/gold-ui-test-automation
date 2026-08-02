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

    // See InventoryPage.AddToCartButtonFor for why this is XPath-by-class-and-text, not data-test.
    private static By RemoveButtonFor(string productName) => By.XPath(
        $"//div[@class='cart_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Remove']");
}
