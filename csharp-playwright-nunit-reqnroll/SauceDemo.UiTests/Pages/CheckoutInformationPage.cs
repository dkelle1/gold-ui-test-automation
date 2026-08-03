using Microsoft.Playwright;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.Pages;

public sealed class CheckoutInformationPage : BasePage
{
    private const string FirstNameInput = "[data-test='firstName']";
    private const string LastNameInput = "[data-test='lastName']";
    private const string PostalCodeInput = "[data-test='postalCode']";
    private const string ContinueButton = "[data-test='continue']";
    private const string ErrorBanner = "[data-test='error']";

    public CheckoutInformationPage(IPage page) : base(page)
    {
    }

    public async Task<CheckoutOverviewPage> FillAndContinueAsync(CheckoutInfo info)
    {
        await TypeAsync(FirstNameInput, info.FirstName);
        await TypeAsync(LastNameInput, info.LastName);
        await TypeAsync(PostalCodeInput, info.PostalCode);
        await ClickAsync(ContinueButton);
        return new CheckoutOverviewPage(Page);
    }

    /// <summary>Leaves the postal code blank and submits, for the "postal code is required" negative scenario.</summary>
    public async Task SubmitWithoutPostalCodeAsync(CheckoutInfo info)
    {
        await TypeAsync(FirstNameInput, info.FirstName);
        await TypeAsync(LastNameInput, info.LastName);
        await ClickAsync(ContinueButton);
    }

    public Task<string> GetErrorMessageAsync() => TextOfAsync(ErrorBanner);

    public Task<bool> HasErrorAsync() => WaitAndCheckVisibleAsync(ErrorBanner);
}
