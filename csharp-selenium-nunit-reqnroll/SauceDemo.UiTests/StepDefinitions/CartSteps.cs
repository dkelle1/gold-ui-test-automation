using NUnit.Framework;
using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class CartSteps
{
    private readonly IWebDriver _driver;
    private readonly ScenarioState _scenarioState;

    public CartSteps(IWebDriver driver, ScenarioState scenarioState)
    {
        _driver = driver;
        _scenarioState = scenarioState;
    }

    [Then(@"the cart badge should show (\d+)")]
    public void ThenTheCartBadgeShouldShow(int expectedCount) =>
        Assert.That(new InventoryPage(_driver).GetCartCount(), Is.EqualTo(expectedCount));

    [When(@"I remove ""(.*)"" from the cart")]
    public void WhenIRemoveFromTheCart(string productName)
    {
        new InventoryPage(_driver).RemoveFromCart(productName);
        _scenarioState.ProductsInCart.Remove(productName);
    }

    [When(@"I start checkout")]
    public void WhenIStartCheckout() => new CartPage(_driver).StartCheckout();

    [Then(@"the cart should list the following products:")]
    public void ThenTheCartShouldListTheFollowingProducts(DataTable table)
    {
        var expected = table.Rows.Select(r => r["ProductName"]).ToList();
        var actual = new CartPage(_driver).ListItemNames();
        Assert.That(actual, Is.EquivalentTo(expected));
    }
}
