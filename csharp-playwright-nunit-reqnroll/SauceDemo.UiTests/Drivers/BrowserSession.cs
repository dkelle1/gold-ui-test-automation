using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace SauceDemo.UiTests.Drivers;

/// <summary>
/// Owns one scenario's whole Playwright stack - driver connection, browser process, isolated context
/// and page - and tears all of it down together. Unlike the Selenium sibling's <c>BrowserSession</c>,
/// there is no temp user-data-dir to create or clean up: a plain (non-persistent) <see cref="IBrowserContext"/>
/// is fully isolated (cookies, local storage, cache) and in-memory by construction, so the entire
/// "avoid concurrent sessions colliding on one profile directory" concern Selenium needed simply
/// doesn't exist here.
///
/// Also accumulates the page's console messages from the moment it's created, because Playwright's
/// console log is push-based (the <see cref="IPage.Console"/> event) rather than a pull-based API like
/// Selenium's <c>IWebDriver.Manage().Logs.GetLog(...)</c> - there is no "give me everything logged so
/// far" call to make after the fact, so something has to have been listening the whole time.
/// </summary>
public sealed class BrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private bool _disposed;

    internal BrowserSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page)
    {
        _playwright = playwright;
        _browser = browser;
        Context = context;
        Page = page;

        Page.Console += (_, message) => ConsoleLog.Enqueue($"[{message.Type}] {message.Text}");
    }

    public IBrowserContext Context { get; }

    public IPage Page { get; }

    /// <summary>Console messages captured over the page's lifetime so far. Best-effort diagnostic data only.</summary>
    public ConcurrentQueue<string> ConsoleLog { get; } = new();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _browser.CloseAsync();
        }
        finally
        {
            _playwright.Dispose();
        }
    }
}
