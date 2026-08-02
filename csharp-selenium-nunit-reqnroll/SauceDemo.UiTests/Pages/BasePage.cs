using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SauceDemo.UiTests.Configuration;

namespace SauceDemo.UiTests.Pages;

/// <summary>
/// Base class for saucedemo page objects. Provides waiting click/type/text helpers so no step
/// definition or page object ever needs `Thread.Sleep` - every interaction waits explicitly for the
/// element to be visible/clickable first, which matters most for `performance_glitch_user`'s
/// artificial ~5s delays.
///
/// Takes only <see cref="IWebDriver"/> - the explicit-wait duration is read from the process-wide
/// <see cref="ConfigurationLoader.Settings"/> rather than threaded through every page's constructor,
/// so BoDi (Reqnroll's DI container) can construct any page object for free as a step-definition
/// constructor parameter: it only needs to resolve IWebDriver, which ScenarioHooks registers.
/// </summary>
public abstract class BasePage
{
    protected readonly IWebDriver Driver;
    protected readonly WebDriverWait Wait;

    protected BasePage(IWebDriver driver)
    {
        Driver = driver;
        var explicitWait = TimeSpan.FromSeconds(ConfigurationLoader.Settings.ExplicitWaitSeconds);
        Wait = new WebDriverWait(driver, explicitWait)
        {
            PollingInterval = TimeSpan.FromMilliseconds(250)
        };
        Wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
    }

    protected IWebElement WaitForVisible(By locator) =>
        Wait.Until(driver =>
        {
            var element = driver.FindElement(locator);
            return element.Displayed ? element : null;
        }) ?? throw new WebDriverTimeoutException($"Element '{locator}' never became visible.");

    protected IWebElement WaitForClickable(By locator) =>
        Wait.Until(driver =>
        {
            var element = driver.FindElement(locator);
            return element.Displayed && element.Enabled ? element : null;
        }) ?? throw new WebDriverTimeoutException($"Element '{locator}' never became clickable.");

    protected void Click(By locator) => WaitForClickable(locator).Click();

    protected void Type(By locator, string text)
    {
        var element = WaitForVisible(locator);
        element.Clear();
        element.SendKeys(text);
    }

    protected string TextOf(By locator) => WaitForVisible(locator).Text;

    protected bool IsVisible(By locator)
    {
        try
        {
            return Driver.FindElement(locator).Displayed;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }
}
