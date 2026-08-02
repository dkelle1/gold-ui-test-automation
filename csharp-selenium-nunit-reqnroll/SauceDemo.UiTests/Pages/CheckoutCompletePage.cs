using OpenQA.Selenium;

namespace SauceDemo.UiTests.Pages;

public sealed class CheckoutCompletePage : BasePage
{
    private static readonly By CompleteHeader = By.CssSelector("[data-test='complete-header']");

    public CheckoutCompletePage(IWebDriver driver) : base(driver)
    {
    }

    public string GetConfirmationMessage() => TextOf(CompleteHeader);
}
