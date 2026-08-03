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
    protected Task WaitForVisibleAsync(string selector) => WaitForVisibleAsync(Page.Locator(selector), selector);

    /// <summary>
    /// Same as <see cref="WaitForVisibleAsync(string)"/>, but for a selector that legitimately matches
    /// several elements - see <see cref="WaitAndCheckAnyVisibleAsync"/> for why that needs its own method.
    /// </summary>
    protected Task WaitForAnyVisibleAsync(string selector) => WaitForVisibleAsync(Page.Locator(selector).First, selector);

    /// <summary>Polls up to the context's default timeout for the element to appear, returning false (never throwing) if it doesn't - for positive assertions ("an error banner should show up") where a brief render delay is expected but permanent absence is a real, reportable outcome rather than a bug in the wait.</summary>
    protected Task<bool> WaitAndCheckVisibleAsync(string selector) => WaitAndCheckVisibleAsync(selector, timeoutMs: null);

    /// <summary>Same as <see cref="WaitAndCheckVisibleAsync(string)"/> but with a caller-supplied timeout instead of the context's full default - for a short, bounded check inside a retry loop, where waiting the full default timeout on every attempt would make the retry pointless.</summary>
    protected Task<bool> WaitAndCheckVisibleAsync(string selector, float? timeoutMs) =>
        WaitAndCheckVisibleAsync(Page.Locator(selector), timeoutMs);

    /// <summary>
    /// Same as <see cref="WaitAndCheckVisibleAsync(string)"/>, but asks only whether the *first* match
    /// is visible, for a selector that is genuinely expected to match several elements (e.g. the six
    /// product-name cells of the inventory grid).
    ///
    /// Playwright's locator actions - <c>WaitForAsync</c> included - run in strict mode by default and
    /// throw a "strict mode violation" as soon as the selector resolves to more than one element,
    /// rather than silently taking the first match the way Selenium's <c>FindElement</c> did. That is a
    /// genuinely useful safety net for every other selector in this project (login inputs, error
    /// banners, buttons - all meant to be unique), so it is deliberately NOT disabled wholesale in
    /// <see cref="WaitAndCheckVisibleAsync(string)"/>/<see cref="WaitForVisibleAsync(string)"/>; the
    /// "many matches are expected, I only need to know at least one rendered" case gets this separate,
    /// explicitly-named method instead.
    /// </summary>
    protected Task<bool> WaitAndCheckAnyVisibleAsync(string selector) =>
        WaitAndCheckVisibleAsync(Page.Locator(selector).First, timeoutMs: null);

    // Which exception type a failed Playwright call actually throws is easy to get wrong, and the two
    // helpers below depend on getting it right:
    //
    //   * A timed-out wait/action throws System.TimeoutException - the BCL type. Microsoft.Playwright
    //     does NOT define a timeout exception of its own (there is no Microsoft.Playwright.TimeoutException),
    //     and System.TimeoutException does NOT derive from PlaywrightException. So `catch (PlaywrightException)`
    //     does not catch a timeout, which is the single most common way one of these calls fails.
    //   * A strict-mode violation (the selector matched more than one element) throws PlaywrightException.
    //
    // That difference is load-bearing rather than pedantic: "the element never showed up" is a normal,
    // reportable outcome that WaitAndCheckVisibleAsync exists to turn into `false`, whereas "this
    // selector is ambiguous" is a bug in the locator that should be surfaced loudly, not absorbed into
    // a bare `false` that reads as an ordinary assertion failure. Hence the deliberately narrow catch.
    private static async Task WaitForVisibleAsync(ILocator locator, string selector)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        }
        catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
        {
            // Both are wrapped here - this helper's job is to fail loudly either way, just with the
            // selector named. The cause is folded into the message rather than left only on
            // InnerException, because Allure reports the top-level message on its own: without this,
            // a report reader sees "never became visible" with no hint whether that was a timeout or
            // an ambiguous selector, which are very different bugs.
            throw new PlaywrightException($"Element '{selector}' never became visible. {ex.Message}", ex);
        }
    }

    private static async Task<bool> WaitAndCheckVisibleAsync(ILocator locator, float? timeoutMs)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs
            });
            return true;
        }
        catch (TimeoutException)
        {
            // Only a timeout means "it never showed up". A PlaywrightException is deliberately left to
            // propagate - see the note above.
            return false;
        }
    }
}
