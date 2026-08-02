using System.Text;
using Allure.Net.Commons;
using OpenQA.Selenium;

namespace SauceDemo.UiTests.Support;

/// <summary>Attaches failure evidence to the current Allure test result. Allure.Net.Commons uses AsyncLocal for its lifecycle context, so this is safe to call concurrently from parallel scenarios.</summary>
public static class AllureAttachments
{
    public static void AttachFailureEvidence(IWebDriver driver)
    {
        AllureApi.AddAttachment("failure-screenshot", "image/png", ScreenshotHelper.Capture(driver), ".png");
        AllureApi.AddAttachment("page-source", "text/html", Encoding.UTF8.GetBytes(driver.PageSource), ".html");
        AllureApi.AddAttachment("final-url", "text/plain", Encoding.UTF8.GetBytes(driver.Url), ".txt");

        TryAttachBrowserConsoleLog(driver);
    }

    private static void TryAttachBrowserConsoleLog(IWebDriver driver)
    {
        try
        {
            var entries = driver.Manage().Logs.GetLog(LogType.Browser);
            if (entries.Count == 0)
            {
                return;
            }

            var text = string.Join(Environment.NewLine, entries.Select(e => $"[{e.Level}] {e.Timestamp:O} {e.Message}"));
            AllureApi.AddAttachment("browser-console-log", "text/plain", Encoding.UTF8.GetBytes(text), ".txt");
        }
        catch (WebDriverException)
        {
            // Not every browser/driver combination exposes the browser log (e.g. geckodriver does not) - best-effort only.
        }
    }
}
