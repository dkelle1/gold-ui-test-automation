using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed partial class CheckoutSteps
{
    private readonly IWebDriver _driver;

    public CheckoutSteps(IWebDriver driver)
    {
        _driver = driver;
    }

    [When(@"I fill in my checkout information")]
    public void WhenIFillInMyCheckoutInformation() =>
        new CheckoutInformationPage(_driver).FillAndContinue(CheckoutDataFactory.Create());

    [When(@"I fill in my checkout information without a postal code")]
    public void WhenIFillInMyCheckoutInformationWithoutAPostalCode()
    {
        var info = CheckoutDataFactory.Create() with { PostalCode = string.Empty };
        new CheckoutInformationPage(_driver).SubmitWithoutPostalCode(info);
    }

    [Then(@"the overview should list (\d+) items")]
    public void ThenTheOverviewShouldListItems(int expectedCount) =>
        Assert.That(
            () => new CheckoutOverviewPage(_driver).ListItemNames().Count,
            Is.EqualTo(expectedCount).After(5000, 250));

    [Then(@"the order total should be a positive dollar amount")]
    public void ThenTheOrderTotalShouldBeAPositiveDollarAmount()
    {
        var totalLabel = new CheckoutOverviewPage(_driver).GetTotalLabel();
        var match = TotalLabelPattern().Match(totalLabel);

        Assert.That(match.Success, Is.True, $"'{totalLabel}' did not match the expected 'Total: $<amount>' format.");
        Assert.That(decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), Is.GreaterThan(0m));
    }

    [When(@"I finish the order")]
    public void WhenIFinishTheOrder() => new CheckoutOverviewPage(_driver).Finish();

    [Then(@"I should see the order confirmation ""(.*)""")]
    public void ThenIShouldSeeTheOrderConfirmation(string expectedMessage) =>
        Assert.That(new CheckoutCompletePage(_driver).GetConfirmationMessage(), Is.EqualTo(expectedMessage));

    [Then(@"I should see the checkout error ""(.*)""")]
    public void ThenIShouldSeeTheCheckoutError(string expectedMessage) =>
        Assert.That(new CheckoutInformationPage(_driver).GetErrorMessage(), Is.EqualTo(expectedMessage));

    [GeneratedRegex(@"^Total: \$(\d+\.\d{2})$")]
    private static partial Regex TotalLabelPattern();
}
