using Microsoft.Playwright;

namespace SauceDemo.UiTests.Pages;

public sealed class LoginPage : BasePage
{
    private const string UsernameInput = "[data-test='username']";
    private const string PasswordInput = "[data-test='password']";
    private const string LoginButton = "[data-test='login-button']";
    private const string ErrorBanner = "[data-test='error']";

    public LoginPage(IPage page) : base(page)
    {
    }

    /// <summary>Submits the login form without asserting the outcome - the caller decides whether success or a specific error is expected.</summary>
    public async Task SubmitLoginAsync(string username, string password)
    {
        await TypeAsync(UsernameInput, username);
        await TypeAsync(PasswordInput, password);
        await ClickAsync(LoginButton);
    }

    public Task<string> GetErrorMessageAsync() => TextOfAsync(ErrorBanner);

    public Task<bool> HasErrorAsync() => WaitAndCheckVisibleAsync(ErrorBanner);
}
