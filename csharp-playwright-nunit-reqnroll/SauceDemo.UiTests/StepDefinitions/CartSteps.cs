using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;
using SauceDemo.UiTests.Pages;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class CartSteps
{
    private readonly IPage _page;

    public CartSteps(IPage page)
    {
        _page = page;
    }

    // NUnit's Is.EqualTo(...).After(...) polling constraint predates async/await and only accepts a
    // synchronous Func<T>. GetAwaiter().GetResult() is safe here - dotnet test's execution context
    // installs no SynchronizationContext, so there is no sync-over-async deadlock risk.
    [Then(@"the cart badge should show (\d+)")]
    public void ThenTheCartBadgeShouldShow(int expectedCount) =>
        Assert.That(
            () => new InventoryPage(_page).GetCartCountAsync().GetAwaiter().GetResult(),
            Is.EqualTo(expectedCount).After(5000, 250));

    [When(@"I remove ""(.*)"" from the cart")]
    public Task WhenIRemoveFromTheCartAsync(string productName) => new InventoryPage(_page).RemoveFromCartAsync(productName);

    [When(@"I try to remove ""(.*)"" from the cart")]
    public Task WhenITryToRemoveFromTheCartAsync(string productName) => new CartPage(_page).RemoveItemAsync(productName);

    [When(@"I start checkout")]
    public async Task WhenIStartCheckoutAsync() => await new CartPage(_page).StartCheckoutAsync();

    [Then(@"the cart should list the following products:")]
    public void ThenTheCartShouldListTheFollowingProducts(DataTable table)
    {
        var expected = table.Rows.Select(r => r["ProductName"]).ToList();
        Assert.That(
            () => new CartPage(_page).ListItemNamesAsync().GetAwaiter().GetResult(),
            Is.EquivalentTo(expected).After(5000, 250));
    }
}
