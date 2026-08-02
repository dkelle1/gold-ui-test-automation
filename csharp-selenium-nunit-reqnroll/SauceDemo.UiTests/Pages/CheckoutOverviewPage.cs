using OpenQA.Selenium;

namespace SauceDemo.UiTests.Pages;

public sealed class CheckoutOverviewPage : BasePage
{
    private static readonly By ItemNames = By.CssSelector("[data-test='inventory-item-name']");
    private static readonly By TotalLabel = By.CssSelector("[data-test='total-label']");
    private static readonly By FinishButton = By.CssSelector("[data-test='finish']");

    public CheckoutOverviewPage(IWebDriver driver) : base(driver)
    {
    }

    public IReadOnlyList<string> ListItemNames() =>
        Driver.FindElements(ItemNames).Select(e => e.Text).ToList();

    public string GetTotalLabel() => TextOf(TotalLabel);

    public CheckoutCompletePage Finish()
    {
        Click(FinishButton);
        return new CheckoutCompletePage(Driver);
    }
}
