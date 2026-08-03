using Microsoft.Playwright;
using SauceDemo.UiTests.Configuration;

namespace SauceDemo.UiTests.Drivers;

/// <summary>
/// Builds one scenario's Playwright stack from <see cref="TestSettings"/>: a fresh driver connection,
/// a fresh browser process, one isolated context, one page. Everything is scenario-scoped - nothing is
/// shared across the concurrently-running NUnit worker threads - deliberately, and not just for
/// symmetry with the Selenium sibling's per-scenario <c>IWebDriver</c>: Playwright's own guidance is
/// that its objects (page/context/browser/the driver connection itself) are not safe to touch from
/// more than one thread at a time, and this project's parallelism (`[assembly: LevelOfParallelism]`
/// in AssemblyInfo.cs) runs scenarios on multiple threads *inside one process*, not one process per
/// worker. A shared "one browser per worker, one context per test" pool - the pattern most Playwright
/// tutorials show - assumes process-per-worker parallelism and would risk exactly the kind of
/// cross-thread access Playwright warns against here. One full stack per scenario sidesteps that
/// entirely, at the cost of a heavier scenario setup than that tutorial pattern - the same trade-off
/// the Selenium sibling already made by launching a whole new browser process per scenario.
///
/// When <see cref="TestSettings.RemoteUrl"/> is set, connects to an already-running Playwright browser
/// server (e.g. `playwright run-server`, or a hosted browser provider) instead of launching locally -
/// no other code changes required, mirroring the Selenium sibling's Selenium Grid / RemoteWebDriver support.
/// </summary>
public sealed class PlaywrightFactory
{
    private readonly TestSettings _settings;

    public PlaywrightFactory(TestSettings settings)
    {
        _settings = settings;
    }

    public async Task<BrowserSession> CreateAsync()
    {
        var playwright = await Playwright.CreateAsync();

        try
        {
            var browser = await LaunchOrConnectAsync(playwright);

            try
            {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    // Playwright always emulates a fixed viewport unless told not to. Headless mode needs
                    // one set explicitly - there's no real window to size otherwise. Headed mode opts out
                    // via NoViewport so --start-maximized (below) governs the size instead, for local
                    // debugging convenience. Notably, headless mode needs no Selenium-style workaround
                    // here: Playwright never resizes a native OS window at all (it sets the viewport
                    // directly), so the 800x600-headless-virtual-screen trap documented in the Selenium
                    // sibling's README structurally cannot happen in this framework.
                    ViewportSize = _settings.Headless
                        ? new ViewportSize { Width = 1920, Height = 1080 }
                        : ViewportSize.NoViewport
                });

                context.SetDefaultTimeout(_settings.ExplicitWaitSeconds * 1000);
                context.SetDefaultNavigationTimeout(_settings.PageLoadTimeoutSeconds * 1000);

                var page = await context.NewPageAsync();

                return new BrowserSession(playwright, browser, context, page);
            }
            catch
            {
                // The browser process already exists at this point - if context/page setup fails, close
                // it rather than leaking a live browser process that nothing will ever dispose.
                try
                {
                    await browser.CloseAsync();
                }
                catch (Exception closeFailure)
                {
                    // Never let a failure while cleaning up replace the exception that caused the
                    // cleanup. A browser that died mid-setup is precisely the case where creating the
                    // context and closing the browser both fail, and surfacing the close failure would
                    // point debugging at teardown instead of at the real cause. The outer catch still
                    // disposes the driver connection, which tears the process down regardless.
                    Console.Error.WriteLine($"Ignoring browser close failure during setup cleanup: {closeFailure.Message}");
                }

                throw;
            }
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    private Task<IBrowser> LaunchOrConnectAsync(IPlaywright playwright)
    {
        var browserType = SelectBrowserType(playwright);

        return string.IsNullOrWhiteSpace(_settings.RemoteUrl)
            ? browserType.LaunchAsync(BuildLaunchOptions())
            : browserType.ConnectAsync(_settings.RemoteUrl);
    }

    private IBrowserType SelectBrowserType(IPlaywright playwright) => _settings.Browser switch
    {
        BrowserType.Chromium => playwright.Chromium,
        BrowserType.Firefox => playwright.Firefox,
        BrowserType.WebKit => playwright.Webkit,
        _ => throw new NotSupportedException($"Unsupported browser: {_settings.Browser}")
    };

    private BrowserTypeLaunchOptions BuildLaunchOptions() => _settings.Browser switch
    {
        BrowserType.Chromium => BuildChromiumLaunchOptions(),
        _ => new BrowserTypeLaunchOptions { Headless = _settings.Headless }
    };

    private BrowserTypeLaunchOptions BuildChromiumLaunchOptions()
    {
        // Required to launch Chromium at all in most CI containers (no --cap-add=SYS_ADMIN, no /dev/shm
        // sized for a real browser) - the same reason the Selenium sibling's WebDriverFactory always
        // applies these for Chrome/Edge. Firefox/WebKit have no equivalent requirement, hence this list
        // is Chromium-only rather than shared across every browser type.
        var args = new List<string> { "--no-sandbox", "--disable-dev-shm-usage" };

        if (!_settings.Headless)
        {
            // Paired with ViewportSize.NoViewport above - this is what actually produces a real maximized
            // window for local, non-headless debugging.
            args.Add("--start-maximized");
        }

        return new BrowserTypeLaunchOptions
        {
            Headless = _settings.Headless,
            Args = args
        };
    }
}
