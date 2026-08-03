using Microsoft.Playwright;
using Reqnroll;
using SauceDemo.UiTests.Pages;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class InventorySteps
{
    private readonly IPage _page;

    public InventorySteps(IPage page)
    {
        _page = page;
    }

    [When(@"I add ""(.*)"" to the cart")]
    public Task WhenIAddToTheCartAsync(string productName) => new InventoryPage(_page).AddToCartAsync(productName);

    [When(@"I add the following products to the cart:")]
    public async Task WhenIAddTheFollowingProductsToTheCartAsync(DataTable table)
    {
        var inventoryPage = new InventoryPage(_page);

        foreach (var row in table.Rows)
        {
            await inventoryPage.AddToCartAsync(row["ProductName"]);
        }
    }

    [When(@"I go to the cart")]
    public async Task WhenIGoToTheCartAsync() => await new InventoryPage(_page).OpenCartAsync();

    [Given(@"my cart is empty")]
    public Task GivenMyCartIsEmptyAsync() => new InventoryPage(_page).ClearCartAsync();
}
