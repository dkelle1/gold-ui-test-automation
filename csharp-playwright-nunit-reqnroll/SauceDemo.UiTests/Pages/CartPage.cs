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

    public Task RemoveItemAsync(string productName) => ClickAsync(RemoveButtonFor(productName));

    public async Task<CheckoutInformationPage> StartCheckoutAsync()
    {
        await ClickAsync(CheckoutButton);
        return new CheckoutInformationPage(Page);
    }

    // See InventoryPage.AddToCartButtonFor for why this is XPath-by-class-and-text, not data-test.
    private static string RemoveButtonFor(string productName) =>
        $"xpath=//div[@class='cart_item'][.//div[@data-test='inventory-item-name' and text()='{productName}']]//button[text()='Remove']";
}
