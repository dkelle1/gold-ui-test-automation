using NUnit.Framework;
using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class CartSteps
{
    private readonly IWebDriver _driver;

    public CartSteps(IWebDriver driver)
    {
        _driver = driver;
    }

    [Then(@"the cart badge should show (\d+)")]
    public void ThenTheCartBadgeShouldShow(int expectedCount) =>
        Assert.That(
            () => new InventoryPage(_driver).GetCartCount(),
            Is.EqualTo(expectedCount).After(5000, 250));

    [When(@"I remove ""(.*)"" from the cart")]
    public void WhenIRemoveFromTheCart(string productName) => new InventoryPage(_driver).RemoveFromCart(productName);

    [When(@"I start checkout")]
    public void WhenIStartCheckout() => new CartPage(_driver).StartCheckout();

    [Then(@"the cart should list the following products:")]
    public void ThenTheCartShouldListTheFollowingProducts(DataTable table)
    {
        var expected = table.Rows.Select(r => r["ProductName"]).ToList();
        Assert.That(
            () => new CartPage(_driver).ListItemNames(),
            Is.EquivalentTo(expected).After(5000, 250));
    }
}
