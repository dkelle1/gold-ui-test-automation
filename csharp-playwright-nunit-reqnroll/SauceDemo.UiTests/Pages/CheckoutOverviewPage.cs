using Microsoft.Playwright;

namespace SauceDemo.UiTests.Pages;

public sealed class CheckoutOverviewPage : BasePage
{
    private const string ItemNames = "[data-test='inventory-item-name']";
    private const string TotalLabel = "[data-test='total-label']";
    private const string FinishButton = "[data-test='finish']";

    public CheckoutOverviewPage(IPage page) : base(page)
    {
    }

    public async Task<IReadOnlyList<string>> ListItemNamesAsync() =>
        await Page.Locator(ItemNames).AllTextContentsAsync();

    public Task<string> GetTotalLabelAsync() => TextOfAsync(TotalLabel);

    public async Task<CheckoutCompletePage> FinishAsync()
    {
        await ClickAsync(FinishButton);
        return new CheckoutCompletePage(Page);
    }
}
