using Microsoft.Playwright;

namespace SauceDemo.UiTests.Pages;

public sealed class CartPage : BasePage
{
    private const string CartItemNames = "[data-test='inventory-item-name']";
    private const string CheckoutButton = "[data-test='checkout']";

    public CartPage(IPage page) : base(page)
    {
    }

    public async Task<IReadOnlyList<string>> ListItemNamesAsync() =>
        await Page.Locator(CartItemNames).AllTextContentsAsync();

    public async Task<CheckoutInformationPage> StartCheckoutAsync()
    {
        await ClickAsync(CheckoutButton);
        return new CheckoutInformationPage(Page);
    }
}
