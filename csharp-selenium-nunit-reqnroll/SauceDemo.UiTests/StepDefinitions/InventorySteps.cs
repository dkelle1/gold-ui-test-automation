using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class InventorySteps
{
    private readonly IWebDriver _driver;

    public InventorySteps(IWebDriver driver)
    {
        _driver = driver;
    }

    [When(@"I add ""(.*)"" to the cart")]
    public void WhenIAddToTheCart(string productName) => new InventoryPage(_driver).AddToCart(productName);

    [When(@"I add the following products to the cart:")]
    public void WhenIAddTheFollowingProductsToTheCart(DataTable table)
    {
        var inventoryPage = new InventoryPage(_driver);

        foreach (var row in table.Rows)
        {
            inventoryPage.AddToCart(row["ProductName"]);
        }
    }

    [When(@"I go to the cart")]
    public void WhenIGoToTheCart() => new InventoryPage(_driver).OpenCart();

    [Given(@"my cart is empty")]
    public void GivenMyCartIsEmpty() => new InventoryPage(_driver).ClearCart();
}
