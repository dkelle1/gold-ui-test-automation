using Microsoft.Playwright;

namespace SauceDemo.UiTests.Pages;

public sealed class CheckoutCompletePage : BasePage
{
    private const string CompleteHeader = "[data-test='complete-header']";

    public CheckoutCompletePage(IPage page) : base(page)
    {
    }

    public Task<string> GetConfirmationMessageAsync() => TextOfAsync(CompleteHeader);
}
