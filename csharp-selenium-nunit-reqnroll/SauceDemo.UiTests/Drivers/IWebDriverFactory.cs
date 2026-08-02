namespace SauceDemo.UiTests.Drivers;

public interface IWebDriverFactory
{
    /// <summary>Creates a new, isolated browser session (local or remote depending on configuration).</summary>
    BrowserSession Create();
}
