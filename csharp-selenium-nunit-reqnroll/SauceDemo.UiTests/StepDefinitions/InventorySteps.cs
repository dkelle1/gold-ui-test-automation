using NUnit.Framework;
using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;

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

    [When(@"I sort products by ""(.*)""")]
    public void WhenISortProductsBy(string sortOptionValue) => new InventoryPage(_driver).SortBy(sortOptionValue);

    /// <summary>
    /// Derives the expected order from <see cref="ProductCatalog.All"/> rather than a literal list, so
    /// the assertion stays correct even if the catalog changes - only the sort *direction* is
    /// hardcoded here, never a specific product ordering.
    /// </summary>
    [Then(@"the products should be listed in (ascending|descending) alphabetical order")]
    public void ThenTheProductsShouldBeListedInAlphabeticalOrder(string direction)
    {
        var expected = direction == "ascending"
            ? ProductCatalog.All.OrderBy(name => name, StringComparer.Ordinal).ToList()
            : ProductCatalog.All.OrderByDescending(name => name, StringComparer.Ordinal).ToList();

        Assert.That(new InventoryPage(_driver).ListProductNames(), Is.EqualTo(expected));
    }
}
