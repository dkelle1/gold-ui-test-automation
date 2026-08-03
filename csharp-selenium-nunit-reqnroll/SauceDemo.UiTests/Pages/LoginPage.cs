using OpenQA.Selenium;

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

    /// <summary>Submits the login form without asserting the outcome - the caller decides whether success or a specific error is expected.</summary>
    public void SubmitLogin(string username, string password)
    {
        Type(UsernameInput, username);
        Type(PasswordInput, password);
        Click(LoginButton);
    }

    public string GetErrorMessage() => TextOf(ErrorBanner);

    public bool HasError() => WaitAndCheckVisible(ErrorBanner);
}
