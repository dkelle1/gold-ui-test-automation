using OpenQA.Selenium;
using SauceDemo.UiTests.TestData;

namespace SauceDemo.UiTests.Pages;

public sealed class CheckoutInformationPage : BasePage
{
    private static readonly By FirstNameInput = By.CssSelector("[data-test='firstName']");
    private static readonly By LastNameInput = By.CssSelector("[data-test='lastName']");
    private static readonly By PostalCodeInput = By.CssSelector("[data-test='postalCode']");
    private static readonly By ContinueButton = By.CssSelector("[data-test='continue']");
    private static readonly By ErrorBanner = By.CssSelector("[data-test='error']");

    public CheckoutInformationPage(IWebDriver driver) : base(driver)
    {
    }

    public CheckoutOverviewPage FillAndContinue(CheckoutInfo info)
    {
        Type(FirstNameInput, info.FirstName);
        Type(LastNameInput, info.LastName);
        Type(PostalCodeInput, info.PostalCode);
        Click(ContinueButton);
        return new CheckoutOverviewPage(Driver);
    }

    /// <summary>Leaves the postal code blank and submits, for the "postal code is required" negative scenario.</summary>
    public void SubmitWithoutPostalCode(CheckoutInfo info)
    {
        Type(FirstNameInput, info.FirstName);
        Type(LastNameInput, info.LastName);
        Click(ContinueButton);
    }

    public string GetErrorMessage() => TextOf(ErrorBanner);

    public bool HasError() => IsVisible(ErrorBanner);
}
