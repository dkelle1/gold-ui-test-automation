using System.Text;
using Allure.Net.Commons;
using SauceDemo.UiTests.Drivers;

namespace SauceDemo.UiTests.Support;

/// <summary>Attaches failure evidence to the current Allure test result. Allure.Net.Commons uses AsyncLocal for its lifecycle context, so this is safe to call concurrently from parallel scenarios.</summary>
public static class AllureAttachments
{
    public static async Task AttachFailureEvidenceAsync(BrowserSession session)
    {
        AllureApi.AddAttachment("failure-screenshot", "image/png", await ScreenshotHelper.CaptureAsync(session.Page), ".png");
        AllureApi.AddAttachment("page-source", "text/html", Encoding.UTF8.GetBytes(await session.Page.ContentAsync()), ".html");
        AllureApi.AddAttachment("final-url", "text/plain", Encoding.UTF8.GetBytes(session.Page.Url), ".txt");

        // Best-effort only, like the Selenium sibling's console-log attachment: unlike Selenium's
        // pull-based Manage().Logs API, Playwright's console log is push-based (BrowserSession
        // subscribes to IPage.Console as soon as the page is created), so there's nothing to catch here
        // beyond "were there any messages at all".
        if (!session.ConsoleLog.IsEmpty)
        {
            var text = string.Join(Environment.NewLine, session.ConsoleLog);
            AllureApi.AddAttachment("browser-console-log", "text/plain", Encoding.UTF8.GetBytes(text), ".txt");
        }
    }
}
