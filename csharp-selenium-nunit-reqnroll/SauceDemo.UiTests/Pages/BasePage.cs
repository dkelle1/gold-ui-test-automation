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

    protected IWebElement WaitForVisible(By locator)
    {
        try
        {
            return Wait.Until(driver =>
            {
                var element = driver.FindElement(locator);
                return element.Displayed ? element : null;
            });
        }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException($"Element '{locator}' never became visible.");
        }
    }

    protected IWebElement WaitForClickable(By locator)
    {
        try
        {
            return Wait.Until(driver =>
            {
                var element = driver.FindElement(locator);
                return element.Displayed && element.Enabled ? element : null;
            });
        }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException($"Element '{locator}' never became clickable.");
        }
    }

    /// <summary>
    /// Clicks the element, retrying the whole find-and-click cycle if the element goes stale between
    /// being located and being clicked (e.g. a re-render triggered by another in-flight interaction) -
    /// the old element reference is unusable once stale, so re-finding it is the only fix.
    /// </summary>
    protected void Click(By locator)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                WaitForClickable(locator).Click();
                return;
            }
            catch (StaleElementReferenceException) when (attempt < maxAttempts)
            {
            }
        }
    }

    protected void Type(By locator, string text)
    {
        var element = WaitForVisible(locator);
        element.Clear();
        element.SendKeys(text);
    }

    protected string TextOf(By locator) => WaitForVisible(locator).Text;

    /// <summary>Instant, non-waiting presence check - correct where absence is a normal outcome (e.g. an empty cart badge), not a race to wait out.</summary>
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
        catch (StaleElementReferenceException)
        {
            // The element was re-rendered between being found and being queried. Callers use this to
            // ask "is it there right now?", and a mid-flight re-render is a legitimate "not right now"
            // - letting it escape would turn a routine race into a scenario failure.
            return false;
        }
    }

    /// <summary>Polls up to the page's explicit wait for the element to appear, returning false (never throwing) if it doesn't - for positive assertions ("an error banner should show up") where a brief render delay is expected but permanent absence is a real, reportable outcome rather than a bug in the wait.</summary>
    protected bool WaitAndCheckVisible(By locator) => WaitAndCheckVisible(locator, Wait.Timeout);

    /// <summary>Same as <see cref="WaitAndCheckVisible(By)"/> but with a caller-supplied timeout instead of the page's full explicit wait - for a short, bounded check inside a retry loop, where waiting the full explicit wait on every attempt would make the retry pointless.</summary>
    protected bool WaitAndCheckVisible(By locator, TimeSpan timeout)
    {
        var wait = new WebDriverWait(Driver, timeout) { PollingInterval = TimeSpan.FromMilliseconds(200) };

        try
        {
            return wait.Until(driver =>
            {
                try
                {
                    return driver.FindElement(locator).Displayed;
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    // Re-rendered mid-poll; this method must never throw, so keep polling instead.
                    return false;
                }
            });
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }
}
