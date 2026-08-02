using NUnit.Framework;
using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class CheckoutSteps
{
    private readonly IWebDriver _driver;
    private readonly ScenarioState _scenarioState;

    public CheckoutSteps(IWebDriver driver, ScenarioState scenarioState)
    {
        _driver = driver;
        _scenarioState = scenarioState;
    }

    [When(@"I fill in my checkout information")]
    public void WhenIFillInMyCheckoutInformation()
    {
        _scenarioState.CheckoutInfo = CheckoutDataFactory.Create();
        new CheckoutInformationPage(_driver).FillAndContinue(_scenarioState.CheckoutInfo);
    }

    [When(@"I fill in my checkout information without a postal code")]
    public void WhenIFillInMyCheckoutInformationWithoutAPostalCode()
    {
        var info = CheckoutDataFactory.Create() with { PostalCode = string.Empty };
        _scenarioState.CheckoutInfo = info;
        new CheckoutInformationPage(_driver).SubmitWithoutPostalCode(info);
    }

    [Then(@"the overview should list (\d+) items")]
    public void ThenTheOverviewShouldListItems(int expectedCount)
    {
        var actual = new CheckoutOverviewPage(_driver).ListItemNames();
        Assert.That(actual, Has.Count.EqualTo(expectedCount));
    }

    [When(@"I finish the order")]
    public void WhenIFinishTheOrder() => new CheckoutOverviewPage(_driver).Finish();

    [Then(@"I should see the order confirmation ""(.*)""")]
    public void ThenIShouldSeeTheOrderConfirmation(string expectedMessage) =>
        Assert.That(new CheckoutCompletePage(_driver).GetConfirmationMessage(), Is.EqualTo(expectedMessage));

    [Then(@"I should see the checkout error ""(.*)""")]
    public void ThenIShouldSeeTheCheckoutError(string expectedMessage) =>
        Assert.That(new CheckoutInformationPage(_driver).GetErrorMessage(), Is.EqualTo(expectedMessage));
}
