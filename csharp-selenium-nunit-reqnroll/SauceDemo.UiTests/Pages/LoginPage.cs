using OpenQA.Selenium;
using SauceDemo.UiTests.Users;

namespace SauceDemo.UiTests.Pages;

public sealed class LoginPage : BasePage
{
    private static readonly By UsernameInput = By.CssSelector("[data-test='username']");
    private static readonly By PasswordInput = By.CssSelector("[data-test='password']");
    private static readonly By LoginButton = By.CssSelector("[data-test='login-button']");
    private static readonly By ErrorBanner = By.CssSelector("[data-test='error']");

    public LoginPage(IWebDriver driver) : base(driver)
    {
    }

    public void Open(string baseUrl) => Driver.Navigate().GoToUrl(baseUrl);

    /// <summary>Logs in with a real, known-good account and follows the redirect to the inventory page.</summary>
    public InventoryPage LoginAs(UserAccount account)
    {
        SubmitLogin(account.Username, account.Password);
        return new InventoryPage(Driver);
    }

    /// <summary>Submits the login form without asserting the outcome, for negative-path scenarios.</summary>
    public void SubmitLogin(string username, string password)
    {
        Type(UsernameInput, username);
        Type(PasswordInput, password);
        Click(LoginButton);
    }

    public string GetErrorMessage() => TextOf(ErrorBanner);

    public bool HasError() => IsVisible(ErrorBanner);
}
