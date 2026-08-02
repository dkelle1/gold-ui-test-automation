using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class InventorySteps
{
    private readonly IWebDriver _driver;
    private readonly ScenarioState _scenarioState;

    public InventorySteps(IWebDriver driver, ScenarioState scenarioState)
    {
        _driver = driver;
        _scenarioState = scenarioState;
    }

    [When(@"I add ""(.*)"" to the cart")]
    public void WhenIAddToTheCart(string productName)
    {
        new InventoryPage(_driver).AddToCart(productName);
        _scenarioState.ProductsInCart.Add(productName);
    }

    [When(@"I add the following products to the cart:")]
    public void WhenIAddTheFollowingProductsToTheCart(DataTable table)
    {
        var inventoryPage = new InventoryPage(_driver);

        foreach (var row in table.Rows)
        {
            var productName = row["ProductName"];
            inventoryPage.AddToCart(productName);
            _scenarioState.ProductsInCart.Add(productName);
        }
    }

    [When(@"I go to the cart")]
    public void WhenIGoToTheCart() => new InventoryPage(_driver).OpenCart();
}
