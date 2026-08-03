using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class LoginSteps
{
    private readonly IPage _page;
    private readonly UserAccount _userAccount;

    public LoginSteps(IPage page, UserAccount userAccount)
    {
        _page = page;
        _userAccount = userAccount;
    }

    [Given(@"I log in with my assigned user")]
    [When(@"I log in with my assigned user")]
    public Task WhenILogInWithMyAssignedUserAsync() =>
        new LoginPage(_page).SubmitLoginAsync(_userAccount.Username, _userAccount.Password);

    [When(@"I log in with username ""(.*)"" and password ""(.*)""")]
    public Task WhenILogInWithUsernameAndPasswordAsync(string username, string password)
    {
        // The "<generated>" marker lets an Examples row ask for fresh, guaranteed-not-real
        // credentials from Bogus instead of a fixed literal.
        if (username == "<generated>" || password == "<generated>")
        {
            var (generatedUsername, generatedPassword) = InvalidCredentialsFactory.Create();
            username = username == "<generated>" ? generatedUsername : username;
            password = password == "<generated>" ? generatedPassword : password;
        }

        return new LoginPage(_page).SubmitLoginAsync(username, password);
    }

    [Then(@"I should land on the inventory page")]
    public async Task ThenIShouldLandOnTheInventoryPageAsync() =>
        Assert.That(await new InventoryPage(_page).IsDisplayedAsync(), Is.True, "Expected the inventory page to be displayed after login.");

    [Then(@"(\d+) products should be listed")]
    public async Task ThenProductsShouldBeListedAsync(int expectedCount)
    {
        var actual = await new InventoryPage(_page).ListProductNamesAsync();
        Assert.That(actual, Has.Count.EqualTo(expectedCount));
        Assert.That(actual, Is.SubsetOf(ProductCatalog.All), "Every listed product name should be a known saucedemo catalog product.");
    }

    [Then(@"I should see the login error ""(.*)""")]
    public async Task ThenIShouldSeeTheLoginErrorAsync(string expectedMessage) =>
        Assert.That(await new LoginPage(_page).GetErrorMessageAsync(), Is.EqualTo(expectedMessage));

    [Then(@"I should see a login error")]
    public async Task ThenIShouldSeeALoginErrorAsync() =>
        Assert.That(await new LoginPage(_page).HasErrorAsync(), Is.True);
}
