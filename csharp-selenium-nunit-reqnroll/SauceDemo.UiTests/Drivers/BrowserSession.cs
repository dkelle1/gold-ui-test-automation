using OpenQA.Selenium;

namespace SauceDemo.UiTests.Drivers;

/// <summary>
/// Owns a WebDriver instance and its dedicated temp profile directory (used to avoid Chrome/Edge's
/// "user data directory is already in use" error when several sessions run concurrently). Disposing
/// quits the browser and best-effort deletes the temp profile; idempotent so a double-dispose from
/// hook ordering mistakes is harmless.
/// </summary>
public sealed class BrowserSession : IDisposable
{
    private readonly string? _userDataDir;
    private bool _disposed;

    public BrowserSession(IWebDriver driver, string? userDataDir)
    {
        Driver = driver;
        _userDataDir = userDataDir;
    }

    public IWebDriver Driver { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            Driver.Quit();
        }
        finally
        {
            Driver.Dispose();
            DeleteUserDataDir();
        }
    }

    private void DeleteUserDataDir()
    {
        if (string.IsNullOrEmpty(_userDataDir) || !Directory.Exists(_userDataDir))
        {
            return;
        }

        try
        {
            Directory.Delete(_userDataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the OS temp directory is reclaimed eventually.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; the OS temp directory is reclaimed eventually.
        }
    }
}
