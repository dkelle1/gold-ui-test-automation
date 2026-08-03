using Microsoft.Playwright;
using SauceDemo.UiTests.Configuration;

namespace SauceDemo.UiTests.Pages;

/// <summary>
/// Base class for saucedemo page objects. Much thinner than the Selenium sibling's BasePage: Playwright
/// locators are lazy (they re-query the DOM on every use, so there is no stale-element concept at all)
/// and every action (click/fill/etc.) auto-waits for the target to be attached, visible, stable and
/// actionable before doing anything - the entire "WaitForClickable then Click, retrying on
/// StaleElementReferenceException" dance that took up most of the Selenium version's BasePage is simply
/// not needed here. What's left is a couple of thin wrappers for the two positive-assertion shapes step
/// definitions actually need: "wait for this or fail loudly" and "wait for this and tell me whether it
/// showed up, without throwing".
///
/// Takes only <see cref="IPage"/> - per-action timeouts are configured once, process-wide, on the
/// scenario's <see cref="IBrowserContext"/> by <see cref="Drivers.PlaywrightFactory"/>, rather than
/// threaded through every page's constructor the way the Selenium sibling threads an explicit-wait
/// TimeSpan through every page object.
/// </summary>
public abstract class BasePage
{
    protected readonly IPage Page;

    protected BasePage(IPage page)
    {
        Page = page;
    }

    protected Task ClickAsync(string selector) => Page.Locator(selector).ClickAsync();

    /// <summary>Fills the field directly (not simulated keystrokes) - saucedemo's forms have no per-keystroke behaviour (autocomplete, incremental validation) that would need real typing, so the faster, less flaky Fill is the right default.</summary>
    protected Task TypeAsync(string selector, string text) => Page.Locator(selector).FillAsync(text);

    protected async Task<string> TextOfAsync(string selector) => await Page.Locator(selector).TextContentAsync() ?? string.Empty;

    /// <summary>Instant, non-waiting presence check - correct where absence is a normal outcome (e.g. an empty cart badge), not a race to wait out.</summary>
    protected Task<bool> IsVisibleAsync(string selector) => Page.Locator(selector).IsVisibleAsync();

    /// <summary>Waits up to the context's default timeout for the element to become visible, throwing a descriptive error if it never does - for preconditions where permanent absence really is a bug (e.g. the post-login product grid never rendering).</summary>
    protected async Task WaitForVisibleAsync(string selector)
    {
        try
        {
            await Page.Locator(selector).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        }
        catch (PlaywrightException ex)
        {
            throw new PlaywrightException($"Element '{selector}' never became visible.", ex);
        }
    }

    /// <summary>Polls up to the context's default timeout for the element to appear, returning false (never throwing) if it doesn't - for positive assertions ("an error banner should show up") where a brief render delay is expected but permanent absence is a real, reportable outcome rather than a bug in the wait.</summary>
    protected Task<bool> WaitAndCheckVisibleAsync(string selector) => WaitAndCheckVisibleAsync(selector, timeoutMs: null);

    /// <summary>Same as <see cref="WaitAndCheckVisibleAsync(string)"/> but with a caller-supplied timeout instead of the context's full default - for a short, bounded check inside a retry loop, where waiting the full default timeout on every attempt would make the retry pointless.</summary>
    protected async Task<bool> WaitAndCheckVisibleAsync(string selector, float? timeoutMs)
    {
        try
        {
            await Page.Locator(selector).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs
            });
            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }
}
