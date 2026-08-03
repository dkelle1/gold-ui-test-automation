using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed partial class CheckoutSteps
{
    private readonly IPage _page;

    public CheckoutSteps(IPage page)
    {
        _page = page;
    }

    [When(@"I fill in my checkout information")]
    public async Task WhenIFillInMyCheckoutInformationAsync() =>
        await new CheckoutInformationPage(_page).FillAndContinueAsync(CheckoutDataFactory.Create());

    [When(@"I fill in my checkout information without a postal code")]
    public Task WhenIFillInMyCheckoutInformationWithoutAPostalCodeAsync()
    {
        var info = CheckoutDataFactory.Create() with { PostalCode = string.Empty };
        return new CheckoutInformationPage(_page).SubmitWithoutPostalCodeAsync(info);
    }

    // See CartSteps.ThenTheCartBadgeShouldShow for why GetAwaiter().GetResult() is used inside this
    // NUnit polling constraint.
    [Then(@"the overview should list (\d+) items")]
    public void ThenTheOverviewShouldListItems(int expectedCount) =>
        Assert.That(
            () => new CheckoutOverviewPage(_page).ListItemNamesAsync().GetAwaiter().GetResult().Count,
            Is.EqualTo(expectedCount).After(5000, 250));

    [Then(@"the order total should be a positive dollar amount")]
    public async Task ThenTheOrderTotalShouldBeAPositiveDollarAmountAsync()
    {
        var totalLabel = await new CheckoutOverviewPage(_page).GetTotalLabelAsync();
        var match = TotalLabelPattern().Match(totalLabel);

        Assert.That(match.Success, Is.True, $"'{totalLabel}' did not match the expected 'Total: $<amount>' format.");
        Assert.That(decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), Is.GreaterThan(0m));
    }

    [When(@"I finish the order")]
    public async Task WhenIFinishTheOrderAsync() => await new CheckoutOverviewPage(_page).FinishAsync();

    [Then(@"I should see the order confirmation ""(.*)""")]
    public async Task ThenIShouldSeeTheOrderConfirmationAsync(string expectedMessage) =>
        Assert.That(await new CheckoutCompletePage(_page).GetConfirmationMessageAsync(), Is.EqualTo(expectedMessage));

    [Then(@"I should see the checkout error ""(.*)""")]
    public async Task ThenIShouldSeeTheCheckoutErrorAsync(string expectedMessage) =>
        Assert.That(await new CheckoutInformationPage(_page).GetErrorMessageAsync(), Is.EqualTo(expectedMessage));

    [GeneratedRegex(@"^Total: \$(\d+\.\d{2})$")]
    private static partial Regex TotalLabelPattern();
}
