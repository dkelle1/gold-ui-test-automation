using NUnit.Framework;
using OpenQA.Selenium;
using Reqnroll;
using SauceDemo.UiTests.Pages;
using SauceDemo.UiTests.TestData;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.StepDefinitions;

[Binding]
public sealed class LoginSteps
{
    private readonly IWebDriver _driver;
    private readonly UserAccount _userAccount;

    public LoginSteps(IWebDriver driver, UserAccount userAccount)
    {
        _driver = driver;
        _userAccount = userAccount;
    }

    [Given(@"I log in with my assigned user")]
    [When(@"I log in with my assigned user")]
    public void WhenILogInWithMyAssignedUser() =>
        new LoginPage(_driver).SubmitLogin(_userAccount.Username, _userAccount.Password);

    [When(@"I log in with username ""(.*)"" and password ""(.*)""")]
    public void WhenILogInWithUsernameAndPassword(string username, string password)
    {
        // The "<generated>" marker lets an Examples row ask for fresh, guaranteed-not-real
        // credentials from Bogus instead of a fixed literal.
        if (username == "<generated>" || password == "<generated>")
        {
            var (generatedUsername, generatedPassword) = InvalidCredentialsFactory.Create();
            username = username == "<generated>" ? generatedUsername : username;
            password = password == "<generated>" ? generatedPassword : password;
        }

        new LoginPage(_driver).SubmitLogin(username, password);
    }

    [Then(@"I should land on the inventory page")]
    public void ThenIShouldLandOnTheInventoryPage() =>
        Assert.That(new InventoryPage(_driver).IsDisplayed(), Is.True, "Expected the inventory page to be displayed after login.");

    [Then(@"(\d+) products should be listed")]
    public void ThenProductsShouldBeListed(int expectedCount)
    {
        var actual = new InventoryPage(_driver).ListProductNames();
        Assert.That(actual, Has.Count.EqualTo(expectedCount));
        Assert.That(actual, Is.SubsetOf(ProductCatalog.All), "Every listed product name should be a known saucedemo catalog product.");
    }

    [Then(@"I should see the login error ""(.*)""")]
    public void ThenIShouldSeeTheLoginError(string expectedMessage) =>
        Assert.That(new LoginPage(_driver).GetErrorMessage(), Is.EqualTo(expectedMessage));

    [Then(@"I should see a login error")]
    public void ThenIShouldSeeALoginError() =>
        Assert.That(new LoginPage(_driver).HasError(), Is.True);

    [When(@"I log out")]
    public void WhenILogOut() => new InventoryPage(_driver).Logout();

    [Then(@"I should land on the login page")]
    public void ThenIShouldLandOnTheLoginPage() =>
        Assert.That(new LoginPage(_driver).IsDisplayed(), Is.True, "Expected the login page to be displayed after logout.");
}
