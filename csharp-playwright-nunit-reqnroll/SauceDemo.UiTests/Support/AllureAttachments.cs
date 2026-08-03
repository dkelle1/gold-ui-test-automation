using System.Text;
using Allure.Net.Commons;
using NUnit.Framework;
using SauceDemo.UiTests.Drivers;

namespace SauceDemo.UiTests.Support;

/// <summary>Attaches failure evidence to the current Allure test result. Allure.Net.Commons uses AsyncLocal for its lifecycle context, so this is safe to call concurrently from parallel scenarios.</summary>
public static class AllureAttachments
{
    /// <summary>
    /// Collects whatever failure evidence the browser can still give us. Each attachment is captured
    /// independently and best-effort, in ascending order of how much of a live browser it needs,
    /// because the scenarios that most need evidence include the ones that failed *because* the page
    /// or browser died - and there a screenshot throws while <see cref="Microsoft.Playwright.IPage.Url"/>
    /// (a locally cached value, no IPC) still answers.
    ///
    /// Nothing here is allowed to throw. This runs from an [AfterScenario] hook, and Reqnroll stops
    /// firing the remaining [AfterScenario] hooks as soon as one throws - so an exception escaping here
    /// would skip the hooks that close the browser and return this scenario's pooled login user,
    /// exhausting the user pool and making every later scenario block until UserAcquireTimeoutSeconds.
    /// Losing a screenshot is a far better outcome than losing the rest of the run.
    /// </summary>
    public static async Task AttachFailureEvidenceAsync(BrowserSession session)
    {
        await TryAttachAsync("final-url", "text/plain", ".txt",
            () => Task.FromResult(Encoding.UTF8.GetBytes(session.Page.Url)));

        await TryAttachAsync("failure-screenshot", "image/png", ".png",
            () => ScreenshotHelper.CaptureAsync(session.Page));

        await TryAttachAsync("page-source", "text/html", ".html",
            async () => Encoding.UTF8.GetBytes(await session.Page.ContentAsync()));

        // Best-effort only, like the Selenium sibling's console-log attachment: unlike Selenium's
        // pull-based Manage().Logs API, Playwright's console log is push-based (BrowserSession
        // subscribes to IPage.Console as soon as the page is created), so there's nothing to catch here
        // beyond "were there any messages at all".
        if (!session.ConsoleLog.IsEmpty)
        {
            await TryAttachAsync("browser-console-log", "text/plain", ".txt",
                () => Task.FromResult(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, session.ConsoleLog))));
        }
    }

    private static async Task TryAttachAsync(string name, string mimeType, string extension, Func<Task<byte[]>> capture)
    {
        try
        {
            AllureApi.AddAttachment(name, mimeType, await capture(), extension);
        }
        catch (Exception ex)
        {
            // Deliberately catches everything - see the contract on AttachFailureEvidenceAsync. The
            // note goes to the test's own captured output so a missing attachment is explainable
            // rather than mysterious.
            TestContext.Out.WriteLine($"Could not attach '{name}' failure evidence: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
